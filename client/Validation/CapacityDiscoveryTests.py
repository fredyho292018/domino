"""Local metadata fixtures only; no authentication, network or real data."""
import unittest,uuid,copy
from CapacityRegistry import Registry

class DiscoveryTests(unittest.TestCase):
    def fixture(self):
        run=str(uuid.UUID(int=51));mid=str(uuid.UUID(int=52));slots=[f'group-01/slot-{i:02}' for i in range(1,5)]
        row=dict(matchId=mid,participantSlots=slots,startedAt='2026-01-01T00:00:00Z',rootStatus='CANCELLED',runtimeStatus='CANCELLED',completedAt='2026-01-01T00:03:00Z')
        data=dict(runId=run,source='AUTHORITATIVE_TEST_DISCOVERY',summary=dict(runId=run,unclassifiedLoadMatches=0,matchIds=[mid],clientMissedMatches=1),matches=[row])
        return Registry(run),mid,slots,data
    def test_missed_assignment_enters_registry_once(self):
        r,mid,slots,d=self.fixture();r.reconcile_discovery(d);r.reconcile_discovery(d)
        self.assertEqual(1,len(r.matches));self.assertEqual('CANCELLED',r.matches[mid]['terminalState'])
    def test_client_and_authority_union(self):
        r,mid,slots,d=self.fixture();r.register(mid,1,slots,'2026-01-01T00:00:00Z');r.reconcile_discovery(d);self.assertEqual(1,len(r.matches))
    def test_unclassified_and_wrong_run_fail_without_update(self):
        for bad in ('run','unknown'):
            r,mid,slots,d=self.fixture()
            if bad=='run':d['runId']=str(uuid.UUID(int=99))
            else:d['summary']['unclassifiedLoadMatches']=1
            with self.assertRaises(ValueError):r.reconcile_discovery(d)
            self.assertEqual({},r.matches)
    def test_missing_authoritative_match_does_not_erase_client_registry(self):
        r,mid,slots,d=self.fixture();r.register(mid,1,slots,'start');d['matches']=[];d['summary']['matchIds']=[]
        with self.assertRaises(ValueError):r.reconcile_discovery(d)
        self.assertIn(mid,r.matches)
if __name__=='__main__':unittest.main()
