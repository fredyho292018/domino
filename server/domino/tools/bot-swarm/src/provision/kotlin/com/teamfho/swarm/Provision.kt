package com.teamfho.swarm

import com.google.auth.oauth2.GoogleCredentials
import com.google.firebase.FirebaseApp
import com.google.firebase.FirebaseOptions
import com.google.firebase.auth.FirebaseAuth
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.future.await
import java.net.URI
import java.net.http.*
import java.nio.file.*
import java.time.Duration

// Explicit administrator tool. No dependency on Domino services/repositories and no gameplay writes.
fun main(args:Array<String>)=runBlocking {
    var stage="CONFIGURATION"
    try {
        val c=Config.load(args);ValidationTarget.authorize(c);val settings=IdentitySettings()
        Files.createDirectories(settings.directory)
        stage="ADMIN_INITIALIZATION"
        val credentials=if(ValidationTarget.emulator())GoogleCredentials.create(com.google.auth.oauth2.AccessToken("emulator-only",java.util.Date(Long.MAX_VALUE)))else GoogleCredentials.getApplicationDefault()
        val app=FirebaseApp.initializeApp(FirebaseOptions.builder().setCredentials(credentials).setProjectId(settings.project).build(),"swarm-provision")
        val auth=FirebaseAuth.getInstance(app)
        HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(15)).build().use{http->
            for(slot in c.slotOffset until c.slotOffset+c.clients) {
                val path=settings.file(slot)
                val data=if(Files.exists(path))Json.read(Files.readString(path))else {
                    stage="CREATE_AUTH_USER"
                    val req=HttpRequest.newBuilder(URI(ValidationTarget.authUrl("identitytoolkit.googleapis.com/v1/accounts:signUp?key="+settings.apiKey)))
                        .timeout(Duration.ofSeconds(30)).header("Content-Type","application/json").POST(HttpRequest.BodyPublishers.ofString("{\"returnSecureToken\":true}")).build()
                    val r=http.sendAsync(req,HttpResponse.BodyHandlers.ofString()).await()
                    if(r.statusCode()!=200)throw SafeFailure("FIREBASE_PROVISION_HTTP_${r.statusCode()}",true)
                    stage="VERIFY_PROJECT"
                    val created=Json.read(r.body());val token=auth.verifyIdToken(created.text("idToken"))
                    require(token.uid==created.text("localId")){"PROVISION_PROJECT_MISMATCH"}
                    stage="SAVE_CREDENTIAL"
                    saveIdentity(path,settings.project,c.environment,token.uid,created.text("refreshToken"))
                    Json.read(Files.readString(path))
                }
                require(data.text("projectId")==settings.project&&data.text("environment")==c.environment&&data.text("testSource")=="BOT_SWARM"&&data.path("isTestAccount").asBoolean()){"PROVISION_SCOPE_MISMATCH"}
                stage="MARK_TEST_IDENTITY"
                val user=auth.getUser(data.text("uid"))
                require(!user.isDisabled){"TEST_IDENTITY_DISABLED"}
                stage="REGISTER_TEST_IDENTITY"
                // This is test metadata only, not a player/wallet/gameplay write. Production clients
                // have no endpoint for asserting this marker; Firebase authentication stays unchanged.
                val db=com.google.firebase.cloud.FirestoreClient.getFirestore(app)
                val ref=db.document("developmentTestAccounts/${user.uid}")
                db.runTransaction {tx->
                    val old=tx.get(ref).get()
                    if(old.exists())require(old.getBoolean("isTestAccount")==true&&old.getString("testSource")=="BOT_SWARM"&&old.getString("environment")==c.environment){"TEST_MARKER_CONFLICT"}
                    else tx.create(ref,mapOf("isTestAccount" to true,"testSource" to "BOT_SWARM","environment" to c.environment,"slot" to slot+1,"createdAt" to com.google.cloud.firestore.FieldValue.serverTimestamp()))
                    true
                }.get()
                println("SWARM_IDENTITY_PROVISIONED slot="+(slot+1))
            }
        }
        app.delete()
    }catch(e:Exception){val category=when(e){is SafeFailure->e.category;is com.google.firebase.auth.FirebaseAuthException->e.authErrorCode?.name?:e.errorCode.name;else->e.javaClass.simpleName};val hint=listOf("quota project","SERVICE_DISABLED","PERMISSION_DENIED","USER_NOT_FOUND","INVALID_ID_TOKEN").firstOrNull{e.message.orEmpty().contains(it,true)}?:"NONE";System.err.println("SWARM_PROVISION_FAILED stage=$stage category=$category hint=$hint");
        val causes=generateSequence<Throwable>(e){it.cause}.take(10).toList()
        val storageCode=causes.filterIsInstance<com.google.api.gax.rpc.ApiException>().firstOrNull()?.statusCode?.code?.name
            ?:causes.filterIsInstance<io.grpc.StatusRuntimeException>().firstOrNull()?.status?.code?.name
        if(storageCode!=null)System.err.println("STORAGE_ERROR="+storageCode)
        kotlin.system.exitProcess(1)}
}
