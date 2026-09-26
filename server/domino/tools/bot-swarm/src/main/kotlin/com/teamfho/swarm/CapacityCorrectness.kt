package com.teamfho.swarm

/** Payload-free counters. Authoritative audit sees full sequence; wire audit permits retransmission. */
class CapacityCorrectness {
    private val counters=mutableMapOf<String,Long>()
    private val applications=mutableSetOf<String>()
    private val receipts=mutableMapOf<String,Long>()
    private var sequence=0L
    private var turn:Int?=null
    fun counters():Map<String,Long> = listOf("sequenceRegressions","sequenceGaps","duplicateEvents","duplicateCommandApplications","invalidTurnAcceptances","unauthorizedDeliveries","idempotencyViolations","commandFailures","unverifiedTurns").associateWith{counters[it]?:0}
    private fun add(k:String){counters[k]=(counters[k]?:0)+1}
    fun authorize(expectedMatch:String,actualMatch:String,seat:Int,visibility:String?,target:Int?) {
        if(expectedMatch!=actualMatch || visibility=="PLAYER_PRIVATE"&&seat!=target || visibility=="SYSTEM_PRIVATE")add("unauthorizedDeliveries")
    }
    fun receipt(id:String,resultSequence:Long,accepted:Boolean) {
        if(!accepted){add("commandFailures");return}
        require(receipts.size<100000 || id in receipts){"AUDIT_BOUND_EXCEEDED"}
        val prior=receipts.putIfAbsent(id,resultSequence)
        if(prior!=null&&prior!=resultSequence)add("idempotencyViolations")
    }
    fun event(seq:Long,type:String,seat:Int?,cause:String?) {
        if(seq<=sequence){add(if(seq==sequence)"duplicateEvents" else "sequenceRegressions");return}
        if(seq!=sequence+1)add("sequenceGaps")
        sequence=seq
        if(type in setOf("TURN_STARTED","TURN_CHANGED"))turn=seat
        if(type in setOf("TILE_PLAYED","PLAYER_PASSED")) {
            if(turn==null)add("unverifiedTurns") else if(seat!=turn)add("invalidTurnAcceptances")
            if(cause!=null) {
                require(applications.size<100000 || cause in applications){"AUDIT_BOUND_EXCEEDED"}
                if(!applications.add(cause))add("duplicateCommandApplications")
            }
        }
    }
}
