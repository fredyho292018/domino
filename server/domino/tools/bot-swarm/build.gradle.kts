plugins { kotlin("jvm"); application }
repositories { mavenCentral() }
kotlin { jvmToolchain(21) }
kotlin.sourceSets.main { kotlin.srcDir("../../validation-common") }
dependencies {
    implementation(platform("org.springframework.boot:spring-boot-dependencies:4.0.8"))
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-core")
    implementation("org.jetbrains.kotlinx:kotlinx-coroutines-jdk8")
    implementation("tools.jackson.core:jackson-databind")
    implementation("org.yaml:snakeyaml")
    testImplementation(kotlin("test-junit5"))
    testRuntimeOnly("org.junit.platform:junit-platform-launcher")
}
application { mainClass.set("com.teamfho.swarm.MainKt") }
tasks.test { useJUnitPlatform() }
tasks.register<JavaExec>("loadAuthSample") {
    dependsOn(tasks.named("classes"))
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.teamfho.swarm.LoadAuthSampleKt")
}
tasks.register<JavaExec>("coordinateIdentities") {
    dependsOn(tasks.named("classes"))
    classpath = sourceSets.main.get().runtimeClasspath
    mainClass.set("com.teamfho.swarm.IdentityCoordinatorKt")
}
// Administrative provisioning is not on the simulated client's runtime classpath.
val provision by sourceSets.creating
configurations[provision.implementationConfigurationName].extendsFrom(configurations.implementation.get())
dependencies {
    add(provision.implementationConfigurationName, "com.google.firebase:firebase-admin:9.10.0")
    add(provision.implementationConfigurationName, sourceSets.main.get().output)
}
tasks.register<JavaExec>("provision") {
    dependsOn(tasks.named(provision.classesTaskName))
    classpath = provision.runtimeClasspath
    mainClass.set("com.teamfho.swarm.ProvisionKt")
}

tasks.register<JavaExec>("inspectMatch") {
    dependsOn(tasks.named(provision.classesTaskName))
    classpath = provision.runtimeClasspath
    mainClass.set("com.teamfho.swarm.InspectMatchKt")
}

tasks.register<JavaExec>("reviewRun") {
    dependsOn(tasks.named(provision.classesTaskName))
    classpath = provision.runtimeClasspath
    mainClass.set("com.teamfho.swarm.ReviewRunKt")
}

tasks.register<JavaExec>("capacityOutcome") {
    dependsOn(tasks.named(provision.classesTaskName))
    classpath = provision.runtimeClasspath
    mainClass.set("com.teamfho.swarm.CapacityOutcomeKt")
}

tasks.register<JavaExec>("capacityDiscover") {
    dependsOn(tasks.named(provision.classesTaskName))
    classpath = provision.runtimeClasspath
    mainClass.set("com.teamfho.swarm.CapacityDiscoverKt")
}

dependencies {
    testImplementation(provision.output)
    testImplementation("com.google.firebase:firebase-admin:9.10.0")
}
tasks.test { useJUnitPlatform { excludeTags("EMULATOR") } }
tasks.register<Test>("capacityDiscoveryEmulatorTest") {
    testClassesDirs = sourceSets.test.get().output.classesDirs
    classpath = sourceSets.test.get().runtimeClasspath
    useJUnitPlatform { includeTags("EMULATOR") }
    environment("FIRESTORE_EMULATOR_HOST", "127.0.0.1:18085")
}
