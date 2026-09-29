import unittest
from S707TimingAnalysis import correlate,summarize
from CapacityMetrics import percentiles

class TimingTests(unittest.TestCase):
    def test_percentiles(self):
        self.assertEqual(percentiles(list(range(1,101))),dict(count=100,p50=50,p95=95,p99=99,max=100))
    def test_pair_before_percentile(self):
        server=[dict(correlation='a',complete=True,phasesNanos={'lookup':2000000},serverTotalNanos=2000000,commandType='PASS')]
        pairs=correlate(server,[dict(correlation='a',clientAckMs=5)])
        self.assertEqual(summarize(pairs)['outsideServer']['p95'],3)
    def test_missing(self):
        self.assertEqual(correlate([], [dict(correlation='missing',clientAckMs=5)]),[])
    def test_negative_residual_not_clamped(self):
        p=dict(clientAckMs=1,serverMs=2,outsideServerMs=-1,commandType='PASS',phasesMs={'lookup':2})
        s=summarize([p]);self.assertEqual(s['negativeResiduals'],1);self.assertEqual(s['outsideServer']['count'],0)
    def test_invalid_phase_sum(self):
        with self.assertRaises(ValueError):correlate([dict(correlation='a',complete=True,phasesNanos={'x':1},serverTotalNanos=2)],[])

if __name__=='__main__':unittest.main()
