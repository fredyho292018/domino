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
    companion object { val CLOSED=AbandonedMatchReconciliationGate { false } }
}

data class LegacyReconciliationFile(val version:Int, val runId:String, val enabled:Boolean, val matchIds:List<String>)

/** All replicas must mount the SAME host directory read-only. No cached open decision.
 * Atomic file replacement opens/closes the gate; absent, unreadable or invalid means closed.
 */
class FileAbandonedMatchReconciliationGate(private val file:Path?, private val expectedRunId:String,
    private val expectedRegistrySha256:String):AbandonedMatchReconciliationGate {
    override fun allowsLegacy(matchId:String):Boolean = try {
        require(file!=null && Files.isRegularFile(file,LinkOption.NOFOLLOW_LINKS) && Files.size(file) in 1..8192)
        val bytes=Files.newInputStream(file).use{it.readNBytes(8193)};require(bytes.size<=8192)
        val config=GameCatalogCodec.mapper.readValue(bytes,LegacyReconciliationFile::class.java)
        require(config.version==1 && config.runId==expectedRunId && UUID.fromString(config.runId).toString()==config.runId)
        require(config.matchIds.size==22 && config.matchIds.distinct().size==22)
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
    fun decision(allowed:Boolean){log.info(if(allowed)"LEGACY_RECONCILIATION_ALLOWED" else "LEGACY_RECONCILIATION_BLOCKED")}
    fun completed(){log.info("LEGACY_RECONCILIATION_COMPLETED")}
}
