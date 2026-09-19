package com.teamfho.swarm

/** Emulator routing is all-or-nothing, exact loopback and a non-production demo project. */
object ValidationTarget {
    const val project="demo-domino-swarm"
    fun emulator(env:Map<String,String> = System.getenv()):Boolean {
        val selected=env["DOMINO_SWARM_EMULATOR"]=="true"
        if(selected) {
            require(env["DOMINO_SWARM_FIREBASE_PROJECT_ID"]==project)
            require(env["FIRESTORE_EMULATOR_HOST"]=="127.0.0.1:18085")
            require(env["FIREBASE_AUTH_EMULATOR_HOST"]=="127.0.0.1:19099")
        } else require(env["FIRESTORE_EMULATOR_HOST"].isNullOrBlank()&&env["FIREBASE_AUTH_EMULATOR_HOST"].isNullOrBlank())
        return selected
    }
    fun authorize(config:Config) {
        if(emulator())require(config.environment=="LOCAL"&&config.baseUrl=="http://127.0.0.1:18086")
        else com.teamfho.domino.validation.RealFirestoreGuard.requireOptIn()
    }
    fun authUrl(path:String)=if(emulator())"http://127.0.0.1:19099/$path" else "https://$path"
}
