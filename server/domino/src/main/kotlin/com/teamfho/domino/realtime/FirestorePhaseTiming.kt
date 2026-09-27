package com.teamfho.domino.realtime

/** TEST trace only. Measures existing calls; never owns/retries an operation. */
class FirestorePhaseTiming(private val clock:()->Long=System::nanoTime) {
    private var preRead=0L
    private var preReads=0
    private var begin:Long?=null
    private var end:Long?=null
    private var invalid=false
    private val attempts=mutableListOf<Attempt>()
    private class Attempt(val begin:Long) {
        var end:Long?=null
        var readNanos=0L
        var reads=0
        var readRounds=0
        var domainNanos=0L
    }
    fun <T> preRead(block:()->T):T {
        val at=clock()
        try{return block()}finally{synchronized(this){preRead+=duration(at,clock());preReads++}}
    }
    fun <T> transaction(block:()->T):T {
        synchronized(this){if(begin!=null)invalid=true;begin=clock()}
        try{return block()}finally{synchronized(this){end=clock()}}
    }
    fun <T> attempt(block:()->T):T {
        val item=Attempt(clock())
        synchronized(this){if(begin==null || attempts.size>=8 || attempts.lastOrNull()?.end==null&&attempts.isNotEmpty())invalid=true;if(attempts.size<8)attempts.add(item)}
        try{return block()}finally{synchronized(this){item.end=clock()}}
    }
    fun <T> read(documentCount:Int=1,block:()->T):T {
        require(documentCount>0)
        val at=clock()
        try{return block()}finally{synchronized(this){
            val item=attempts.lastOrNull()
            if(item==null || item.end!=null)invalid=true else {item.readNanos+=duration(at,clock());item.reads+=documentCount;item.readRounds++}
        }}
    }
    fun <T> domain(block:()->T):T {
        val at=clock()
        try{return block()}finally{synchronized(this){
            val item=attempts.lastOrNull()
            if(item!=null && item.end==null)item.domainNanos+=duration(at,clock())
        }}
    }
    private fun duration(a:Long,b:Long):Long {if(b<a)invalid=true;return (b-a).coerceAtLeast(0)}
    @Synchronized fun snapshot():Map<String,Any> {
        val callbacks=attempts.map {duration(it.begin,it.end?:it.begin)}
        val total=if(begin!=null&&end!=null)duration(begin!!,end!!)else 0L
        val acquire=if(begin!=null&&attempts.isNotEmpty())duration(begin!!,attempts.first().begin)else 0L
        val gaps=attempts.zipWithNext().sumOf{(a,b)->duration(a.end?:a.begin,b.begin)}
        val completion=if(attempts.isNotEmpty()&&end!=null)duration(attempts.last().end?:attempts.last().begin,end!!)else 0L
        val reads=attempts.sumOf{it.readNanos};val domain=attempts.sumOf{it.domainNanos}
        val local=callbacks.sum()-reads-domain
        val complete=!invalid && preReads==1 && begin!=null&&end!=null&&attempts.isNotEmpty()&&attempts.all{it.end!=null} &&
            local>=0 && acquire+gaps+callbacks.sum()+completion==total
        return mapOf("complete" to complete,"preReadNanos" to preRead,"preReadCount" to preReads,
            "transactionTotalNanos" to total,"acquisitionToFirstCallbackNanos" to acquire,
            "transactionReadNanos" to reads,"transactionReadCount" to attempts.sumOf{it.reads},
            "transactionReadRoundCount" to attempts.sumOf{it.readRounds},
            "domainNanos" to domain,"callbackLocalOtherNanos" to local.coerceAtLeast(0),
            "betweenAttemptsNanos" to gaps,"completionTailNanos" to completion,
            "postCommitFirestoreNanos" to 0L,"postCommitFirestoreOperationCount" to 0,
            "totalNanos" to preRead+total,"attemptCount" to attempts.size,"retryCount" to (attempts.size-1).coerceAtLeast(0))
    }
}
