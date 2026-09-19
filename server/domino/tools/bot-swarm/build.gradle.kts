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
