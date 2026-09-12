package com.teamfho.domino.config

import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.Firestore
import com.google.firebase.FirebaseApp
import com.google.firebase.FirebaseOptions
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.cloud.FirestoreClient
import org.springframework.boot.autoconfigure.condition.ConditionalOnProperty
import org.springframework.boot.context.properties.EnableConfigurationProperties
import org.springframework.context.annotation.Bean
import org.springframework.context.annotation.Configuration

@Configuration(proxyBeanMethods = false)
@EnableConfigurationProperties(FirebaseProperties::class)
class FirebaseAdminConfiguration {

    @Configuration(proxyBeanMethods = false)
    @ConditionalOnProperty(prefix = "firebase", name = ["enabled"], havingValue = "true", matchIfMissing = true)
    class EnabledConfiguration {

        @Bean(destroyMethod = "delete")
        fun firebaseApp(properties: FirebaseProperties): FirebaseApp {
            val options = FirebaseOptions.builder()
                .setCredentials(GoogleCredentials.getApplicationDefault())
                .setProjectId(properties.projectId)
                .build()
            // Own this named app; never reuse/delete an app belonging to another context.
            return FirebaseApp.initializeApp(options, "domino-backend")
        }

        @Bean(destroyMethod = "")
        fun firebaseAuth(firebaseApp: FirebaseApp): FirebaseAuth = FirebaseAuth.getInstance(firebaseApp)

        // FirebaseApp.delete() disposes its Firestore service. Disable Spring's inferred
        // close() to give the SDK a single owner for shutdown.
        @Bean(destroyMethod = "")
        fun firestore(firebaseApp: FirebaseApp): Firestore = FirestoreClient.getFirestore(firebaseApp)
    }
}
