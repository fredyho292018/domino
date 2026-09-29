"""Offline only. Pair samples before computing residual; never subtract percentiles."""
from CapacityMetrics import percentiles

def correlate(server,client):
    accepted={}
    for row in server:
        if not row['complete']:continue
        key=row['correlation']
        if key in accepted:raise ValueError('DUPLICATE_SERVER_CORRELATION')
        if sum(row['phasesNanos'].values())!=row['serverTotalNanos']:raise ValueError('PHASE_SUM')
        if min(row['phasesNanos'].values())<0:raise ValueError('NEGATIVE_PHASE')
        accepted[key]=row
    pairs=[];seen=set()
    for row in client:
        key=row['correlation']
        if key in seen:raise ValueError('DUPLICATE_CLIENT_CORRELATION')
        seen.add(key)
        if key not in accepted:continue
        s=accepted[key]
        residual=row['clientAckMs']-s['serverTotalNanos']/1e6
        pairs.append(dict(correlation=key,clientAckMs=row['clientAckMs'],serverMs=s['serverTotalNanos']/1e6,
            outsideServerMs=residual,commandType=s['commandType'],phasesMs={k:v/1e6 for k,v in s['phasesNanos'].items()}))
    return pairs

def summarize(pairs):
    # Negative residuals remain counted, not clamped into an apparent successful measurement.
    return {'paired':len(pairs),'negativeResiduals':sum(p['outsideServerMs']<0 for p in pairs),
        'client':percentiles([p['clientAckMs'] for p in pairs]),'server':percentiles([p['serverMs'] for p in pairs]),
        'outsideServer':percentiles([p['outsideServerMs'] for p in pairs if p['outsideServerMs']>=0]),
        'phases':{k:percentiles([p['phasesMs'][k] for p in pairs]) for k in (pairs[0]['phasesMs'] if pairs else [])},
        'commandTypes':{k:percentiles([p['clientAckMs'] for p in pairs if p['commandType']==k]) for k in sorted({p['commandType'] for p in pairs})}}
