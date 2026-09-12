package com.teamfho.domino.config

import com.google.auth.oauth2.AccessToken
import com.google.auth.oauth2.GoogleCredentials
import com.google.cloud.firestore.Firestore
import com.google.firebase.FirebaseApp
import com.google.firebase.auth.FirebaseAuth
import com.google.firebase.cloud.FirestoreClient
import org.junit.jupiter.api.Test
import org.junit.jupiter.params.ParameterizedTest
import org.junit.jupiter.params.provider.ValueSource
import org.mockito.Mockito
import org.springframework.boot.test.context.runner.ApplicationContextRunner
import java.util.Date
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertSame
import kotlin.test.assertTrue

class FirebaseAdminConfigurationTests {
    private val runner = ApplicationContextRunner()
        .withUserConfiguration(FirebaseAdminConfiguration::class.java)

    @Test
    fun `properties bind and disabled configuration never requests ADC`() {
        Mockito.mockStatic(GoogleCredentials::class.java).use { adc ->
            runner.withPropertyValues("firebase.project-id=domino-test", "firebase.enabled=false").run { context ->
                assertNull(context.startupFailure)
                val properties = context.getBean(FirebaseProperties::class.java)
                assertEquals("domino-test", properties.projectId)
                assertFalse(properties.enabled)
                assertTrue(context.getBeansOfType(FirebaseApp::class.java).isEmpty())
                assertTrue(context.getBeansOfType(FirebaseAuth::class.java).isEmpty())
                assertTrue(context.getBeansOfType(Firestore::class.java).isEmpty())
                adc.verifyNoInteractions()
            }
        }
    }

    @Test
    fun `project ID is required before ADC is attempted`() {
        Mockito.mockStatic(GoogleCredentials::class.java).use { adc ->
            runner.run { context ->
                assertNotNull(context.startupFailure)
                assertTrue(context.startupFailure.toString().contains("firebase"))
                adc.verifyNoInteractions()
            }
        }
    }

    @ParameterizedTest
    @ValueSource(strings = ["", " ", "Bad-Project", "short", "project-", "project/invalid"])
    fun `invalid project ID fails before ADC`(projectId: String) {
        Mockito.mockStatic(GoogleCredentials::class.java).use { adc ->
            runner.withPropertyValues("firebase.project-id=$projectId").run { context ->
                assertNotNull(context.startupFailure)
                adc.verifyNoInteractions()
            }
        }
    }

    @Test
    fun `enabled configuration owns one app and cleans registry on context close`() {
        // An in-memory OAuth token is never refreshed or sent. No ADC lookup or network.
        val credentials = GoogleCredentials.create(AccessToken("test-only-not-a-real-token", Date(Long.MAX_VALUE)))
        val firestore = Mockito.mock(Firestore::class.java)
        Mockito.mockStatic(GoogleCredentials::class.java).use { adc ->
            adc.`when`<GoogleCredentials> { GoogleCredentials.getApplicationDefault() }.thenReturn(credentials)
            Mockito.mockStatic(FirestoreClient::class.java).use { store ->
                store.`when`<Firestore> { FirestoreClient.getFirestore(Mockito.any(FirebaseApp::class.java)) }
                    .thenReturn(firestore)
                repeat(2) {
                    assertTrue(FirebaseApp.getApps().none { app -> app.name == "domino-backend" })
                    runner.withPropertyValues("firebase.project-id=domino-test").run { context ->
                        assertNull(context.startupFailure)
                        val app = context.getBean(FirebaseApp::class.java)
                        assertSame(app, context.getBean(FirebaseApp::class.java))
                        assertEquals("domino-test", app.options.projectId)
                        assertTrue(context.getBean(FirebaseProperties::class.java).enabled)
                        assertEquals(1, context.getBeansOfType(FirebaseApp::class.java).size)
                        assertEquals(1, FirebaseApp.getApps().count { it.name == "domino-backend" })
                        assertSame(FirebaseAuth.getInstance(app), context.getBean(FirebaseAuth::class.java))
                        assertSame(firestore, context.getBean(Firestore::class.java))
                    }
                    assertTrue(FirebaseApp.getApps().none { app -> app.name == "domino-backend" })
                }
                adc.verify({ GoogleCredentials.getApplicationDefault() }, Mockito.times(2))
                store.verify({ FirestoreClient.getFirestore(Mockito.any(FirebaseApp::class.java)) }, Mockito.times(2))
                // Only FirebaseApp owns disposal of SDK services; Spring must not call close again.
                Mockito.verifyNoInteractions(firestore)
            }
        }
    }
}
