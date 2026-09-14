package com.teamfho.domino.online

import com.teamfho.domino.catalog.GameCatalogCodec
import java.nio.file.Files
import java.nio.file.Path

object OnlineFixtureExporter {
    @JvmStatic fun main(args: Array<String>) {
        val f=OnlineFixture();f.start();repeat(8){f.move()}
        val out=Path.of("../../client/Validation/Generated").toAbsolutePath().normalize();Files.createDirectories(out)
        for(seat in 0..1)Files.writeString(out.resolve("i1-snapshot-$seat.json"),GameCatalogCodec.mapper.writeValueAsString(f.service.snapshot("p$seat",f.id)))
        Files.writeString(out.resolve("i1-wire-message.json"),GameCatalogCodec.mapper.writeValueAsString(mapOf(
            "type" to "MATCH_UPDATE","version" to 1,"sequence" to 1,"timestamp" to java.time.Instant.now().toString(),
            "payload" to mapOf("matchId" to f.id,"firstSequence" to 1,"events" to f.repo.events(f.id,0).map {OnlineMatchService.authorized(it,0)},"snapshot" to f.service.snapshot("p0",f.id)))))
        println("I1_PRESENTATION_FIXTURES=EXPORTED")
    }
}
