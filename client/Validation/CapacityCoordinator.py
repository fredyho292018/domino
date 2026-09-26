"""R2 measurement consumer only. No launch/deploy/network capability; policy remains a proposal."""
import math
from CapacityMetrics import StopPolicy

def merge_histograms(histograms):
    buckets={};count=0;overflow=0;maximum=None
    for h in histograms:
        count+=h['count'];overflow+=h['overflow']
        if h['count']:maximum=max(maximum or 0,h['maxMs'])
        for k,v in h['bucketsMs'].items():buckets[int(k)]=buckets.get(int(k),0)+v
    if sum(buckets.values())+overflow!=count:raise ValueError('HISTOGRAM_COUNT_MISMATCH')
    def percentile(p):
        if not count:return None
        rank=math.ceil(count*p/100);total=0
        for k,v in sorted(buckets.items()):
            total+=v
            if total>=rank:return k
        return 'OVER_60000_MS'
    return {'count':count,'p50':percentile(50),'p95':percentile(95),'p99':percentile(99),'max':maximum,'overflow':overflow}

class Coordinator:
    def __init__(self):self.policy=StopPolicy();self.baseline=None;self.previous5xx=0
    def frame(self,resource,groups,registry):
        api=resource['containers']['cuban-domino-api'];redis=resource['redis']
        if not api['cpuValid'] or resource.get('HOST_CPU_PERCENT') is None or not resource.get('jvmFresh'):return {'stop':'RESOURCE_COVERAGE_MISSING','canAdvance':False}
        if self.baseline is None:self.baseline=redis.copy()
        counts={k:sum(g.get(k,0) for g in groups) for k in ('unauthorizedDeliveries','sequenceRegressions','duplicateCommandApplications','invalidTurnAcceptances','idempotencyViolations','commandFailures','activeAuthenticatedSockets','http5xx')}
        for match in registry.snapshot()['matches']:
            for key,value in match.get('correctness',{}).items():
                if key in counts:counts[key]+=value
            if match.get('correctness',{}).get('sequenceGaps',0):counts['sequenceRegressions']+=1
        recent5xx=counts['http5xx']-self.previous5xx
        if recent5xx<0:return {'stop':'HTTP_COUNTER_RESET','canAdvance':False}
        self.previous5xx=counts['http5xx']
        ack=merge_histograms([g['commandAckLatency'] for g in groups])
        def upper(value):return 60001 if value=='OVER_60000_MS' else value or 0
        s=dict(cpuQuotaPercent=api['CPU_QUOTA_PERCENT'],hostCpuPercent=resource['HOST_CPU_PERCENT'],hostAvailableBytes=resource['hostAvailableBytes'],
               apiMemoryPercent=100*api['memoryBytes']/int(api['memoryMax']),restart=any(c['restarts'] for c in resource['containers'].values()),
               oom=any(c['oom'] for c in resource['containers'].values()),unavailable=any(not c['running'] or c['health']!='healthy' for c in resource['containers'].values()),
               redisRejection=redis['errorstat_OOM']>self.baseline['errorstat_OOM'] or redis['rejected_connections']>self.baseline['rejected_connections'],
               redisEviction=redis['evicted_keys']>self.baseline['evicted_keys'],unauthorized=counts['unauthorizedDeliveries']>0,
               sequenceCorruption=counts['sequenceRegressions']>0,duplicateApplication=counts['duplicateCommandApplications']>0,
               invalidTurn=counts['invalidTurnAcceptances']>0,matchCorruption=counts['idempotencyViolations']>0,
               http5xx=recent5xx,ackCount=ack['count'],ackP95=upper(ack['p95']),ackP99=upper(ack['p99']))
        stop=self.policy.evaluate(resource['monotonic'],s)
        return {'stop':stop,'canAdvance':False,'reviewApprovalRequired':True,'activeWs':counts['activeAuthenticatedSockets'],
                'outcomes':registry.snapshot()['counts'],'ack':ack,'correctness':counts}
