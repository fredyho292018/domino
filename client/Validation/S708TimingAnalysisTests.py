import unittest
from S708TimingAnalysis import percentiles,validate_detail
from CapacityCoordinator import merge_histograms

class Tests(unittest.TestCase):
    def test_rank_and_bucket(self):
        s=percentiles([1.999]*94+[2.001]*5+[10.9])
        self.assertEqual((s['count'],s['p50'],s['p95'],s['p99'],s['max']),(100,1,2,2,10))
    def test_merge_equivalent(self):
        h=[dict(count=3,overflow=0,maxMs=3,bucketsMs={'1':2,'3':1}),dict(count=2,overflow=0,maxMs=4,bucketsMs={'2':1,'4':1})]
        self.assertEqual(merge_histograms(h),percentiles([1,1,3,2,4]))
    def test_overflow(self):
        self.assertEqual(percentiles([60001])['p95'],'OVER_60000_MS')
    def test_missing(self):self.assertIsNone(percentiles([])['p95'])
    def test_invalid(self):
        for v in [-1,float('nan'),float('inf')]:
            with self.assertRaises(ValueError):percentiles([v])
    def test_uid_field_rejected(self):
        with self.assertRaisesRegex(ValueError,'UNEXPECTED_METRIC_FIELD'):validate_detail({'uid':'private-uid'})
    def test_credential_field_rejected(self):
        with self.assertRaisesRegex(ValueError,'UNEXPECTED_METRIC_FIELD'):validate_detail({'token':'private-token'})
if __name__=='__main__':unittest.main()
