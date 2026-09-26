package com.teamfho.swarm

import java.time.Instant
import java.util.UUID

/** TEST-only metadata policy. Identity values never appear in its output. */
data class AuthorityMatch(val id:String,val createdAt:Instant,val players:Set<String>,val participantCount:Int,
    val status:String,val runtimeStatus:String?,val validationData:Boolean,val pendingWork:Boolean,val finishedAt:String?=null)
data class RunScope(val runId:String,val start:Instant,val through:Instant,val loadPlayers:Set<String>,val clientIds:Set<String>) {
    init { require(UUID.fromString(runId).toString()==runId);require(loadPlayers.size==100);require(through>=start);require(java.time.Duration.between(start,through).toHours()<24) }
}
data class RegistryDiscovery(val runId:String,val clientObservedMatches:Int,val authoritativeDiscoveredMatches:Int,
    val registryUnionMatches:Int,val clientMissedMatches:Int,val unclassifiedLoadMatches:Int,
    val activeLoadMatches:Int,val baselineReady:Boolean,val matchIds:List<String>,val excludedIds:List<String>,val unclassifiedIds:List<String>)
object AuthoritativeRunRegistry {
    val active=setOf("CREATED","STARTING","IN_PROGRESS")
    fun discover(scope:RunScope,records:List<AuthorityMatch>):RegistryDiscovery {
        val byId=records.groupBy{it.id};require(byId.values.all{it.distinct().size==1}){"CONFLICTING_AUTHORITY_READS"}
        val matches=byId.values.map{it.first()};val included=mutableSetOf<String>();val unknown=mutableSetOf<String>();val excluded=mutableSetOf<String>();var activeLoad=0
        for(m in matches) {
            require(UUID.fromString(m.id).toString()==m.id)
            val touches=m.players.any{it in scope.loadPlayers}
            val consistent=m.status==m.runtimeStatus && m.status in active+setOf("FINISHED","CANCELLED")
            if(touches && (m.status in active || m.runtimeStatus in active || m.pendingWork))activeLoad++
            val inWindow=m.createdAt>=scope.start && m.createdAt<=scope.through
            val member=m.participantCount==4 && m.players.size==4 && scope.loadPlayers.containsAll(m.players) && m.validationData
            if(touches && (!consistent || !member))unknown+=m.id
            if(member && inWindow && consistent)included+=m.id else excluded+=m.id
            if(m.id in scope.clientIds && !(member && inWindow && consistent))unknown+=m.id
        }
        unknown+=scope.clientIds-byId.keys
        require(included.size<=25){"MATCH_26_OR_SCOPE_CONTAMINATION"}
        return RegistryDiscovery(scope.runId,scope.clientIds.size,included.size,included.size,(included-scope.clientIds).size,unknown.size,
            activeLoad,activeLoad==0&&unknown.isEmpty(),included.sorted(),excluded.sorted(),unknown.sorted())
    }
}

/** Final reads continue beyond client STOP: late durable creation must still be discovered.
 * Caller must first confirm all clients exited; minimum 360s spans in-flight work/recovery.
 * No finite observation proves absence of arbitrarily delayed external writes.
 */
class RegistryFinalization(private val stoppedAt:Instant) {
    private var previous:RegistryDiscovery?=null;private var observedAt:Instant?=null
    fun observe(at:Instant,scope:RunScope,records:List<AuthorityMatch>):Pair<RegistryDiscovery,Boolean> {
        require(at>=stoppedAt && scope.through==at);require(observedAt==null||at>observedAt)
        val next=AuthoritativeRunRegistry.discover(scope,records)
        val ready=at>=stoppedAt.plusSeconds(360) && previous==next && next.baselineReady && observedAt!=null && at>=observedAt!!.plusSeconds(2)
        previous=next;observedAt=at;return next to ready
    }
}
