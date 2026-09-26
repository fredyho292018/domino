package com.teamfho.domino.online

import com.teamfho.domino.catalog.GameCatalogCodec
import org.slf4j.LoggerFactory
import java.nio.file.Files
import java.nio.file.LinkOption
import java.nio.file.Path
import java.security.MessageDigest
import java.util.UUID

/** Legacy authorization is operational, never a client field or a global match query. */
fun interface AbandonedMatchReconciliationGate {
    fun allowsLegacy(matchId:String):Boolean
    fun allowsLegacy(state:OnlineState):Boolean=allowsLegacy(state.match.matchId)
    fun singleton():Boolean=false
    fun operationId():String="UNCONFIGURED"
    companion object { val CLOSED=AbandonedMatchReconciliationGate { false } }
}

data class LegacyReconciliationFile(val version:Int, val runId:String, val enabled:Boolean, val matchIds:List<String>, val mode:String="EXACT_22")

/** All replicas must mount the SAME host directory read-only. No cached open decision.
 * Atomic file replacement opens/closes the gate; absent, unreadable or invalid means closed.
 */
class FileAbandonedMatchReconciliationGate(private val file:Path?, private val expectedRunId:String,
    private val expectedRegistrySha256:String, private val mode:String="EXACT_22",
    private val singletonMatchId:String=""):AbandonedMatchReconciliationGate {
    override fun singleton()=mode=="SINGLE_MATCH"
    override fun operationId()=runCatching{UUID.fromString(expectedRunId).toString().takeIf{it==expectedRunId}}.getOrNull()?:"UNCONFIGURED"
    override fun allowsLegacy(state:OnlineState):Boolean =
        (!singleton() || state.match.status==com.teamfho.domino.match.MatchStatus.IN_PROGRESS) && allowsLegacy(state.match.matchId)
    override fun allowsLegacy(matchId:String):Boolean = try {
        require(file!=null && Files.isRegularFile(file,LinkOption.NOFOLLOW_LINKS) && Files.size(file) in 1..8192)
        val bytes=Files.newInputStream(file).use{it.readNBytes(8193)};require(bytes.size<=8192)
        val config=GameCatalogCodec.mapper.readValue(bytes,LegacyReconciliationFile::class.java)
        require(config.version==1 && config.runId==expectedRunId && UUID.fromString(config.runId).toString()==config.runId)
        require(config.mode==mode && mode in setOf("EXACT_22","SINGLE_MATCH"))
        val count=if(singleton())1 else 22
        require(config.matchIds.size==count && config.matchIds.distinct().size==count)
        if(singleton())require(UUID.fromString(singletonMatchId).toString()==singletonMatchId && config.matchIds.single()==singletonMatchId)
        require(config.matchIds.all{UUID.fromString(it).toString()==it})
        require(expectedRegistrySha256.matches(Regex("[0-9a-f]{64}")) && registryHash(config.matchIds)==expectedRegistrySha256)
        config.enabled && matchId in config.matchIds
    } catch(_:Exception) { false }
    companion object {
        fun registryHash(ids:List<String>):String = MessageDigest.getInstance("SHA-256")
            .digest((ids.sorted().joinToString("\n")+"\n").toByteArray(Charsets.UTF_8)).joinToString(""){"%02x".format(it)}
    }
}

object LegacyReconciliationAudit {
    private val log=LoggerFactory.getLogger(LegacyReconciliationAudit::class.java)
    fun fingerprint(id:String)=MessageDigest.getInstance("SHA-256").digest(id.toByteArray(Charsets.UTF_8)).joinToString(""){"%02x".format(it)}
    fun decision(allowed:Boolean,operation:String,matchId:String){log.info("{} operation={} matchFingerprint={}",if(allowed)"LEGACY_RECONCILIATION_ALLOWED" else "LEGACY_RECONCILIATION_BLOCKED",operation,fingerprint(matchId))}
    fun completed(operation:String,matchId:String){log.info("LEGACY_RECONCILIATION_COMPLETED operation={} matchFingerprint={}",operation,fingerprint(matchId))}
}
