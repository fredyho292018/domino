import java.lang.instrument.Instrumentation;
import java.lang.management.ManagementFactory;
import java.nio.file.*;
import java.nio.file.attribute.PosixFilePermissions;
import javax.management.*;

/** Explicit local TEST sampler: bounded lifespan, no transforms or remote management. */
public final class S707TimingAgent {
    private static volatile boolean running;
    public static synchronized void agentmain(String options, Instrumentation unused) throws Exception {
        if(running)throw new IllegalStateException("ALREADY_RUNNING");
        if(!options.matches("/tmp/domino-s707-[a-z0-9-]+\\.jsonl"))throw new IllegalArgumentException("OUTPUT");
        Path output=Path.of(options);
        Files.createFile(output,PosixFilePermissions.asFileAttribute(PosixFilePermissions.fromString("rw-------")));
        MBeanServer server=ManagementFactory.getPlatformMBeanServer();
        ObjectName name=new ObjectName("com.teamfho.domino:type=AckPhaseTiming");
        server.invoke(name,"start",new Object[]{1800},new String[]{"int"});
        running=true;
        Files.writeString(output,"{\"configuration\":"+(String)server.invoke(name,"configuration",new Object[0],new String[0])+"}\n",StandardOpenOption.APPEND);
        Thread thread=new Thread(()->{
            try {
                for(int n=0;n<900 && Files.exists(output);n++) {
                    String data=(String)server.invoke(name,"drain",new Object[0],new String[0]);
                    var threads=ManagementFactory.getThreadMXBean();
                    String row="{\"timestampMillis\":"+System.currentTimeMillis()+",\"threadCount\":"+threads.getThreadCount()+",\"peakThreadCount\":"+threads.getPeakThreadCount()+",\"timing\":"+data+"}\n";
                    Files.writeString(output,row,StandardOpenOption.APPEND);
                    Thread.sleep(2000);
                }
            }catch(Exception e){/* Never print exception data. Missing samples stop the experiment. */}
            finally {
                try {server.invoke(name,"stop",new Object[0],new String[0]);}catch(Exception ignored){}
                running=false;
            }
        },"s707-bounded-timing");thread.setDaemon(true);thread.start();
    }
}
