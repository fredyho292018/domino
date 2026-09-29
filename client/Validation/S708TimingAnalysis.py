"""Offline 1-ms histogram metrics, preserving established bucket and overflow semantics."""
import math,re
from CapacityCoordinator import merge_histograms
from S707TimingAnalysis import correlate

def percentiles(values):
    buckets={};overflow=0;maximum=None;count=0
    for value in values:
        if not math.isfinite(value) or value<0:raise ValueError('INVALID_LATENCY')
        ms=int(value);count+=1;maximum=max(maximum or 0,ms)
        if ms>60000:overflow+=1
        else:buckets[str(ms)]=buckets.get(str(ms),0)+1
    return merge_histograms([dict(count=count,overflow=overflow,maxMs=maximum,bucketsMs=buckets)])

def summarize(pairs):
    return dict(paired=len(pairs),negativeResiduals=sum(p['outsideServerMs']<0 for p in pairs),
        client=percentiles([p['clientAckMs'] for p in pairs]),server=percentiles([p['serverMs'] for p in pairs]),
        outsideServer=percentiles([p['outsideServerMs'] for p in pairs if p['outsideServerMs']>=0]),
        phases={k:percentiles([p['phasesMs'][k] for p in pairs]) for k in (pairs[0]['phasesMs'] if pairs else [])},
        commandTypes={k:percentiles([p['clientAckMs'] for p in pairs if p['commandType']==k]) for k in sorted({p['commandType'] for p in pairs})})

def validate_detail(row):
    if set(row)!={'correlation','commandType','complete','firestoreDetail','phasesNanos','serverTotalNanos','transactionInclusiveNanos','attemptCount','retryCount','outboundQueueNanos','completedEpochMillis'}:raise ValueError('UNEXPECTED_METRIC_FIELD')
    if not re.fullmatch('[0-9a-f]{64}',row['correlation']):raise ValueError('UNSAFE_CORRELATION')
    if row['commandType'] not in {'PLAY_TILE','PASS','NEXT_ROUND','SELECT_STARTER_TILE','SUBMIT_EVEN_ODD_GUESS'}:raise ValueError('UNSAFE_COMMAND_TYPE')
    d=row['firestoreDetail']
    allowed={'complete','preReadNanos','preReadCount','transactionTotalNanos','acquisitionToFirstCallbackNanos','transactionReadNanos','transactionReadCount','domainNanos','callbackLocalOtherNanos','betweenAttemptsNanos','completionTailNanos','postCommitFirestoreNanos','postCommitFirestoreOperationCount','totalNanos','attemptCount','retryCount'}
    if set(d)!=allowed or any(not isinstance(v,(int,bool)) for v in d.values()):raise ValueError('UNSAFE_DETAIL')
    if not d['complete']:raise ValueError('INCOMPLETE_FIRESTORE')
    if any(v<0 for k,v in d.items() if k.endswith('Nanos')):raise ValueError('NEGATIVE_FIRESTORE')
    if sum(d[k] for k in ['acquisitionToFirstCallbackNanos','transactionReadNanos','domainNanos','callbackLocalOtherNanos','betweenAttemptsNanos','completionTailNanos'])!=d['transactionTotalNanos']:raise ValueError('TRANSACTION_SUM')
    if d['preReadNanos']+d['transactionTotalNanos']!=d['totalNanos'] or d['totalNanos']>row['serverTotalNanos']:raise ValueError('TOTAL_SUM')
    if d['attemptCount']!=row['attemptCount'] or d['retryCount']!=d['attemptCount']-1:raise ValueError('ATTEMPT_SUM')
    return d
