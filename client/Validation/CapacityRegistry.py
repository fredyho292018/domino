"""Credential-free run registry and terminal-state accounting. Exact allowlists only."""
import re
import uuid

class Registry:
    def __init__(self,run_id):self.run_id=str(uuid.UUID(run_id));self.matches={}
    def register(self,match_id,group,slots,start):
        match_id=str(uuid.UUID(match_id))
        if not 1<=group<=5 or len(slots)!=4 or len(set(slots))!=4 or any(not re.fullmatch(r'group-0[1-5]/slot-(0[1-9]|1[0-9]|20)',s) for s in slots):raise ValueError('INVALID_SLOT_REFERENCES')
        old=self.matches.get(match_id)
        record={'matchId':match_id,'group':group,'participantSlots':sorted(slots),'startedAt':start,'terminalState':'UNKNOWN','completedAt':None}
        if old:
            if any(old[k]!=record[k] for k in ('group','participantSlots')):raise ValueError('REGISTRY_CONFLICT')
            return
        if len(self.matches)>=25:raise ValueError('MATCH_BOUND_EXCEEDED')
        if any(set(slots)&set(x['participantSlots']) for x in self.matches.values()):raise ValueError('SECOND_MATCH_FOR_SLOT')
        self.matches[match_id]=record
    def authoritative(self,match_id,root_status,runtime_status,finished_at):
        record=self.matches[match_id]
        state=classify(root_status,runtime_status)
        if record['terminalState'] in ('COMPLETED','CANCELLED','FAILED') and state!=record['terminalState']:raise ValueError('TERMINAL_STATE_REGRESSION')
        if state in ('COMPLETED','CANCELLED') and not finished_at:raise ValueError('MISSING_TERMINAL_TIME')
        record.update(terminalState=state,completedAt=finished_at if state in ('COMPLETED','CANCELLED','FAILED') else None,source='AUTHORITATIVE_READ')
    def snapshot(self):return {'runId':self.run_id,'matches':list(self.matches.values()),'counts':{s:sum(x['terminalState']==s for x in self.matches.values()) for s in ('COMPLETED','CANCELLED','FAILED','ACTIVE','UNKNOWN')}}
    def ingest(self, outcome):
        if outcome.get('runId')!=self.run_id or outcome.get('source')!='AUTHORITATIVE_TEST_READ':raise ValueError('UNTRUSTED_OUTCOME')
        if outcome['terminalState']!=classify(outcome['rootStatus'],outcome['runtimeStatus']):raise ValueError('OUTCOME_CONFLICT')
        self.authoritative(outcome['matchId'],outcome['rootStatus'],outcome['runtimeStatus'],outcome.get('completedAt'))
        self.matches[outcome['matchId']]['correctness']=dict(outcome['correctness'])
    def replay_sample(self):
        ids=sorted(k for k,v in self.matches.items() if v['terminalState']=='COMPLETED' and v.get('source')=='AUTHORITATIVE_READ')
        if len(ids)<5:raise ValueError('FIVE_COMPLETED_REQUIRED')
        return ids[:5]
    def reconcile_discovery(self, discovery):
        """Union authoritative IDs, including assignments never logged by a client.

        Consumers must call discovery before baseline, at stages and after STOP;
        a client-only snapshot cannot establish registry completeness.
        """
        if discovery.get('runId')!=self.run_id or discovery.get('source')!='AUTHORITATIVE_TEST_DISCOVERY':raise ValueError('UNTRUSTED_DISCOVERY')
        summary=discovery['summary'];rows=discovery['matches']
        if summary.get('runId')!=self.run_id or summary['unclassifiedLoadMatches']!=0:raise ValueError('UNCLASSIFIED_LOAD_MATCHES')
        ids=[r['matchId'] for r in rows]
        if len(set(ids))!=len(ids) or set(ids)!=set(summary['matchIds']) or len(ids)>25:raise ValueError('DISCOVERY_SCOPE_MISMATCH')
        if not set(self.matches)<=set(ids):raise ValueError('DISCOVERY_LOST_REGISTERED_MATCH')
        # Work on a copy so a bad late row cannot partially update the registry.
        import copy
        staged=copy.deepcopy(self)
        for row in rows:
            slots=row['participantSlots']
            group=min(int(s.split('/')[0].split('-')[1]) for s in slots)
            if row['matchId'] in staged.matches:
                group=staged.matches[row['matchId']]['group']
            staged.register(row['matchId'],group,slots,row['startedAt'])
            staged.authoritative(row['matchId'],row['rootStatus'],row['runtimeStatus'],row.get('completedAt'))
        self.matches=staged.matches
        return dict(summary)

def classify(root,runtime):
    if root!=runtime:return 'UNKNOWN'
    return {'FINISHED':'COMPLETED','CANCELLED':'CANCELLED','CREATED':'ACTIVE','STARTING':'ACTIVE','IN_PROGRESS':'ACTIVE'}.get(root,'UNKNOWN')
