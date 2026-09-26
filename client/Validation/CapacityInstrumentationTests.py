import unittest,uuid
from CapacityMetrics import cpu,quota,host_cpu,percentiles,StopPolicy
from CapacityRegistry import Registry,classify
from CapacityCoordinator import Coordinator,merge_histograms

class Tests(unittest.TestCase):
    def test_cpu(self):
        self.assertEqual(cpu(0,0,2,4)['CPU_QUOTA_PERCENT'],0)
        self.assertEqual(cpu(0,2000000,2,4)['CPU_QUOTA_PERCENT'],25)
        self.assertEqual(cpu(0,8000000,2,4)['CPU_QUOTA_PERCENT'],100)
        for args in ((5,4,2,4),(0,1,0,4),(0,1,-1,4),(0,1,float('nan'),4),(0,1,2,0)):
            with self.assertRaises(ValueError):cpu(*args)
        self.assertEqual(quota('400000 100000',8),4)
        self.assertEqual(quota('max 100000',8),8)
        self.assertEqual(quota('400000 100000',2),2)
        for text in ('0 100000','400000 0','nonsense'):
            with self.assertRaises(ValueError):quota(text,8)
        self.assertEqual(host_cpu([0]*10,[25,0,25,50,0,0,0,0,900,900]),50)
    def test_percentiles(self):
        self.assertEqual(percentiles([])['p99'],None)
        self.assertEqual(percentiles([7])['p99'],7)
        self.assertEqual(percentiles(range(1,101)),dict(count=100,p50=50,p95=95,p99=99,max=100))
    def test_stop_windows(self):
        s=dict(cpuQuotaPercent=96,hostCpuPercent=30,hostAvailableBytes=4*1024**3,apiMemoryPercent=25)
        p=StopPolicy()
        for t in range(0,30,2):self.assertIsNone(p.evaluate(t,s))
        self.assertEqual(p.evaluate(30,s),'API_CPU')
        p=StopPolicy();self.assertIsNone(p.evaluate(0,s));self.assertIsNone(p.evaluate(2,{**s,'cpuQuotaPercent':0}))
        self.assertIsNone(p.evaluate(4,s));self.assertEqual(p.evaluate(12,s),'STALE_OR_INVALID_SAMPLE')
        self.assertEqual(StopPolicy().evaluate(0,{**s,'unauthorized':True}),'UNAUTHORIZED')
        self.assertEqual(StopPolicy().evaluate(0,{}),'MISSING_RESOURCE_METRIC')
        self.assertIsNone(StopPolicy().evaluate(0,{**s,'cpuQuotaPercent':0,'dockerCpuPercent':900}))
    def test_registry(self):
        r=Registry(str(uuid.uuid4()))
        for g in range(1,7):
            mid=str(uuid.UUID(int=g));group=min(g,5)
            slots=[f'group-{group:02}/slot-{s:02}' for s in range(5 if g==6 else 1,9 if g==6 else 5)]
            r.register(mid,group,slots,'2026-01-01T00:00:00Z')
            r.authoritative(mid,'CANCELLED' if g==6 else 'FINISHED','CANCELLED' if g==6 else 'FINISHED','2026-01-01T00:01:00Z')
        self.assertEqual(len(r.replay_sample()),5);self.assertNotIn(str(uuid.UUID(int=6)),r.replay_sample())
        self.assertEqual(r.snapshot()['counts']['CANCELLED'],1)
        self.assertEqual(classify('FINISHED','IN_PROGRESS'),'UNKNOWN')
        with self.assertRaises(ValueError):r.authoritative(str(uuid.UUID(int=1)),'IN_PROGRESS','IN_PROGRESS',None)
    def test_merged_histograms(self):
        h=lambda values:dict(count=len(values),overflow=0,maxMs=max(values),bucketsMs={str(x):values.count(x) for x in set(values)})
        merged=merge_histograms([h(list(range(1,51))),h(list(range(51,101)))])
        self.assertEqual((merged['p50'],merged['p95'],merged['p99'],merged['max']),(50,95,99,100))
        self.assertIsNone(merge_histograms([])['p99'])
        with self.assertRaises(ValueError):merge_histograms([dict(count=2,overflow=0,maxMs=1,bucketsMs={'1':1})])
    def test_coordinator_5xx_is_recent_and_audit_is_joined(self):
        c=Coordinator();r=Registry(str(uuid.uuid4()))
        state=dict(cpuValid=True,CPU_QUOTA_PERCENT=10,memoryBytes=1,memoryMax=100,restarts=0,oom=False,running=True,health='healthy')
        resource=dict(containers={'cuban-domino-api':state},redis=dict(errorstat_OOM=0,rejected_connections=0,evicted_keys=0),HOST_CPU_PERCENT=10,hostAvailableBytes=4*1024**3,jvmFresh=True)
        group=dict(http5xx=1,commandAckLatency=dict(count=0,overflow=0,bucketsMs={}))
        for t in range(0,20,2):self.assertIsNone(c.frame({**resource,'monotonic':t},[group],r)['stop'])
        mid=str(uuid.uuid4());r.register(mid,1,[f'group-01/slot-{i:02}' for i in range(1,5)],'now')
        record=dict(runId=r.run_id,source='AUTHORITATIVE_TEST_READ',matchId=mid,terminalState='ACTIVE',rootStatus='IN_PROGRESS',runtimeStatus='IN_PROGRESS',correctness={'invalidTurnAcceptances':1})
        r.ingest(record)
        self.assertEqual(c.frame({**resource,'monotonic':20},[group],r)['stop'],'INVALIDTURN')
        with self.assertRaises(ValueError):r.ingest({**record,'runId':str(uuid.uuid4())})
    def test_persistent_5xx_window_and_recovery(self):
        s=dict(cpuQuotaPercent=10,hostCpuPercent=10,hostAvailableBytes=4*1024**3,apiMemoryPercent=10,http5xx=1)
        p=StopPolicy()
        for t in range(0,10,2):self.assertIsNone(p.evaluate(t,s))
        self.assertEqual(p.evaluate(10,s),'PERSISTENT_5XX')
        p=StopPolicy();p.evaluate(0,s);p.evaluate(2,{**s,'http5xx':0})
        for t in range(4,14,2):self.assertIsNone(p.evaluate(t,s))
        self.assertEqual(p.evaluate(14,s),'PERSISTENT_5XX')

if __name__=='__main__':unittest.main()
