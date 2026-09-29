import importlib.util,pathlib,unittest,json,uuid,sys
sys.dont_write_bytecode=True
spec=importlib.util.spec_from_file_location('p','client/Validation/Server7Provision.py');p=importlib.util.module_from_spec(spec);spec.loader.exec_module(p)
class Checks(unittest.TestCase):
 def setUp(self):
  d=pathlib.Path('client/Validation/Generated/SERVER7P/tests')/str(uuid.uuid4());d.mkdir(parents=True);p.LOAD=d/'LOAD';p.OUT=d/'out';p.OUT.mkdir();(p.LOAD/'group-01').mkdir(parents=True)
 def slot(self,n,uid='synthetic-load-1',project='teamfho-domino'):
  (p.LOAD/'group-01'/f'slot-{n:02}.json').write_text(json.dumps(dict(projectId=project,environment='TEST',testSource='BOT_SWARM',isTestAccount=True,uid=uid,refreshToken='synthetic-fixture')))
 def test_distinct(self):
  self.slot(1);self.slot(2,'synthetic-load-2');self.assertEqual(len(p.records()),2)
 def test_duplicates(self):
  self.slot(1);self.slot(2)
  with self.assertRaises(AssertionError):p.records()
 def test_project_guard(self):
  self.slot(1,project='not-test')
  with self.assertRaises(AssertionError):p.records()
 def test_functional_exclusion(self):
  self.slot(1);(p.LOAD.parent/'TEST').mkdir();(p.LOAD.parent/'TEST/slot-01.json').write_text(json.dumps({'uid':'synthetic-load-1'}))
  with self.assertRaises(AssertionError):p.records()
 def test_pending_batch_blocks_retry(self):
  (p.LOAD/'group-01/batch-01.pending').write_text('{}');sys.argv=['test','provision']
  p.invoke=lambda *a: self.fail('Unexpected remote call')
  with self.assertRaises(AssertionError):p.main()
 def test_partial_failure_stops(self):
  sys.argv=['test','provision'];calls=[]
  def failure(*a):calls.append(a);return 1,[]
  p.invoke=failure
  with self.assertRaises(RuntimeError):p.main()
  self.assertEqual(len(calls),1);self.assertTrue((p.LOAD/'group-01/batch-01.pending').exists())
if __name__=='__main__':unittest.main()

