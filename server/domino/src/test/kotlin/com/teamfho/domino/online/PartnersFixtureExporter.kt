package com.teamfho.domino.online

import com.teamfho.domino.catalog.*
import com.teamfho.domino.match.*
import java.nio.file.Files
import java.nio.file.Path
import java.time.Instant

object PartnersFixtureExporter {
    @JvmStatic fun main(args:Array<String>) {
        val out=Path.of("../../client/Validation/Generated").toAbsolutePath().normalize()
        Files.writeString(out.resolve("m5-catalog.json"),GameCatalogCodec.json(GameCatalogValidator.resolve(GameCatalogV4Publisher.canonical())))
        var s=PartnersOnlineTests.started();val engine=OnlineEngine()
        repeat(8){i->val seat=s.match.currentSeat!!;val hand=s.hands.getValue("$seat");val move=hand.firstNotNullOfOrNull{t->ChainEnd.entries.firstOrNull{OnlineEngine.fits(s,t,it)}?.let{t to it}}
            s=engine.command(s,"m5-p$seat",OnlineCommand(1,"move-$i",s.match.matchId,if(move==null)OnlineCommandType.PASS else OnlineCommandType.PLAY_TILE,move?.first,move?.second),Instant.EPOCH).state}
        for(seat in 0..3)Files.writeString(out.resolve("m5-snapshot-$seat.json"),GameCatalogCodec.json(OnlineMatchService.snapshot(s,"m5-p$seat")))
        println("M5_PRESENTATION_FIXTURES=EXPORTED")
    }
}
