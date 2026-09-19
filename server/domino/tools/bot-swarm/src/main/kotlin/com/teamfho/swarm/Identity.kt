package com.teamfho.swarm

import kotlinx.coroutines.future.await
import java.net.URI
import java.net.URLEncoder
import java.net.http.*
import java.nio.channels.FileChannel
import java.nio.file.*
import java.time.Duration

class IdentitySettings(env:Map<String,String> = System.getenv()) {
    val directory=Path.of(requireNotNull(env["DOMINO_SWARM_IDENTITIES_DIR"]){"IDENTITIES_DIRECTORY_REQUIRED"}).toAbsolutePath().normalize()
    val project=requireNotNull(env["DOMINO_SWARM_FIREBASE_PROJECT_ID"]){"FIREBASE_PROJECT_REQUIRED"}
    val apiKey=requireNotNull(env["DOMINO_SWARM_FIREBASE_API_KEY"]){"FIREBASE_API_KEY_REQUIRED"}
    init {
        require(project.matches(Regex("[a-z0-9-]+"))&&apiKey.matches(Regex("[A-Za-z0-9_-]+"))){"INVALID_FIREBASE_CONFIGURATION"}
        require(generateSequence(directory){it.parent}.none{Files.exists(it.resolve(".git"))}){"CREDENTIALS_MUST_BE_OUTSIDE_REPOSITORY"}
    }
    fun file(slot:Int)=directory.resolve("slot-%02d.json".format(slot+1))
}

// Refresh tokens remain in a local, unversioned directory. A slot lease prevents simultaneous reuse.
class Identity(private val settings:IdentitySettings,private val config:Config,val slot:Int,private val http:HttpClient):AutoCloseable {
    private val lockFile=FileChannel.open(settings.file(slot).resolveSibling("slot-%02d.lock".format(slot+1)),StandardOpenOption.CREATE,StandardOpenOption.WRITE)
    private val lease=try {lockFile.tryLock()?:throw SafeFailure("IDENTITY_SLOT_IN_USE",true)}
        catch(e:Exception){lockFile.close();throw e}
    var uid:String="";private set
    var token:String="";private set
    private var expiresAt=0L
    suspend fun currentToken():String {if(System.currentTimeMillis()+60000>=expiresAt)refresh();return token}
    suspend fun refresh() {
        val file=settings.file(slot);val saved=Json.read(Files.readString(file))
        require(saved.text("projectId")==settings.project&&saved.text("environment")==config.environment&&saved.path("isTestAccount").asBoolean()&&saved.text("testSource")=="BOT_SWARM"){"IDENTITY_SCOPE_MISMATCH"}
        val body="grant_type=refresh_token&refresh_token="+URLEncoder.encode(saved.text("refreshToken"),Charsets.UTF_8)
        val req=HttpRequest.newBuilder(URI(ValidationTarget.authUrl("securetoken.googleapis.com/v1/token?key="+settings.apiKey))).timeout(Duration.ofSeconds(30))
            .header("Content-Type","application/x-www-form-urlencoded").POST(HttpRequest.BodyPublishers.ofString(body)).build()
        val r=http.sendAsync(req,HttpResponse.BodyHandlers.ofString()).await()
        if(r.statusCode()!=200)throw SafeFailure("FIREBASE_REFRESH_FAILED",r.statusCode() in 400..499)
        val value=Json.read(r.body());val nextUid=value.text("user_id")
        require(nextUid==saved.text("uid")&&(uid.isEmpty()||uid==nextUid)){"IDENTITY_CHANGED"}
        uid=nextUid;token=value.text("id_token");require(token.isNotBlank())
        // Local guard only; backend still cryptographically verifies the ID token.
        val claims=Json.read(String(java.util.Base64.getUrlDecoder().decode(token.split('.')[1]),Charsets.UTF_8))
        require(claims.text("aud")==settings.project){"FIREBASE_PROJECT_MISMATCH"}
        expiresAt=claims.path("exp").asLong()*1000
        val refreshed=value.text("refresh_token")
        if(refreshed.isNotBlank()&&refreshed!=saved.text("refreshToken"))saveIdentity(file,settings.project,config.environment,uid,refreshed)
    }
    override fun close(){try{if(lease.isValid)lease.release()}finally{lockFile.close();token=""}}
}

fun saveIdentity(file:Path,project:String,environment:String,uid:String,refreshToken:String) {
    val temporary=file.resolveSibling(file.fileName.toString()+".next")
    Files.writeString(temporary,Json.write(mapOf("projectId" to project,"environment" to environment,"uid" to uid,"refreshToken" to refreshToken,"isTestAccount" to true,"testSource" to "BOT_SWARM")))
    Files.move(temporary,file,StandardCopyOption.ATOMIC_MOVE,StandardCopyOption.REPLACE_EXISTING)
}
