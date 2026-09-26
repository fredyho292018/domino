"""Pure S7-02 calculations and fail-closed R2 policy. No load launcher or network access."""
import math


def quota(cpu_max, allowed_cpus):
    fields=cpu_max.split()
    if len(fields)!=2 or int(fields[1])<=0 or allowed_cpus<=0: raise ValueError('INVALID_QUOTA')
    if fields[0]=='max':return float(allowed_cpus)
    if int(fields[0])<=0:raise ValueError('INVALID_QUOTA')
    return min(int(fields[0])/int(fields[1]),float(allowed_cpus))


def cpu(previous,current,elapsed,limit):
    if not math.isfinite(elapsed) or elapsed<=0 or not math.isfinite(limit) or limit<=0:raise ValueError('INVALID_INTERVAL_OR_QUOTA')
    if current<previous:raise ValueError('COUNTER_RESET')
    cores=(current-previous)/1_000_000/elapsed
    return {'CPU_CORES_USED':cores,'CPU_QUOTA_PERCENT':100*cores/limit}


def host_cpu(previous,current):
    # guest/guest_nice are already included in user/nice; exclude those duplicated columns.
    delta=[b-a for a,b in zip(previous[:8],current[:8])]
    if len(delta)!=8 or min(delta)<0 or sum(delta)<=0:raise ValueError('INVALID_HOST_COUNTERS')
    return 100*(sum(delta)-delta[3]-delta[4])/sum(delta)


def percentiles(values):
    data=sorted(values)
    if any(not math.isfinite(x) or x<0 for x in data):raise ValueError('INVALID_LATENCY')
    return {'count':len(data),**{f'p{p}':data[math.ceil(len(data)*p/100)-1] if data else None for p in (50,95,99)},'max':max(data) if data else None}


class StopPolicy:
    """Proposal only. Every sample is monotonic, no Docker CPUPerc dependency."""
    def __init__(self):self.since={};self.previous=None
    def evaluate(self,t,s):
        if self.previous is not None and (t<=self.previous or t-self.previous>6):return 'STALE_OR_INVALID_SAMPLE'
        self.previous=t
        for key in ('restart','oom','redisRejection','redisEviction','unavailable','unauthorized','sequenceCorruption','matchCorruption','duplicateApplication','invalidTurn'):
            if s.get(key,False):return key.upper()
        if any(k not in s or not math.isfinite(s[k]) for k in ('cpuQuotaPercent','hostCpuPercent','hostAvailableBytes','apiMemoryPercent')):return 'MISSING_RESOURCE_METRIC'
        if s['hostAvailableBytes']<1536*1024*1024:return 'HOST_MEMORY'
        rules={'API_CPU':(s['cpuQuotaPercent']>=95,30),'HOST_CPU':(s['hostCpuPercent']>=95,30),
               'API_MEMORY':(s['apiMemoryPercent']>=90,30),'PERSISTENT_5XX':(s.get('http5xx',0)>0,10),
               'ACK_LATENCY':(s.get('ackCount',0)>=100 and (s.get('ackP95',0)>1000 or s.get('ackP99',0)>3000),30)}
        for name,(bad,duration) in rules.items():
            if bad:
                self.since.setdefault(name,t)
                if t-self.since[name]>=duration:return name
            else:self.since.pop(name,None)
        return None
