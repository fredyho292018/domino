import java.lang.instrument.Instrumentation;
import java.lang.management.*;
import java.nio.file.*;
import java.nio.file.attribute.PosixFilePermissions;

/** Bounded allowlisted JVM counters only. No bytecode changes, sockets, flags or credentials. */
public final class S702JvmAgent {
    private static volatile boolean running;
    public static synchronized void agentmain(String options, Instrumentation unused) throws Exception {
        if (running) throw new IllegalStateException("SAMPLER_ALREADY_RUNNING");
        String[] fields=options.split(",",-1);
        if (fields.length>2 || !fields[0].matches("/tmp/domino-s702-[a-z0-9-]+\\.json")) throw new IllegalArgumentException("INVALID_OUTPUT");
        int samples=fields.length==2?Integer.parseInt(fields[1]):31;
        if(samples<2 || samples>1801)throw new IllegalArgumentException("INVALID_SAMPLE_BOUND");
        Path path=Path.of(fields[0]);
        Files.createFile(path,PosixFilePermissions.asFileAttribute(PosixFilePermissions.fromString("rw-------")));
        running=true;
        Thread worker=new Thread(()->{
            try {
                long next=System.nanoTime();
                for(int i=0;i<samples;i++) {
                    Files.writeString(path,sample(),StandardOpenOption.TRUNCATE_EXISTING);
                    next+=2_000_000_000L;
                    long delay=next-System.nanoTime();if(delay>0)Thread.sleep(delay/1_000_000L);
                }
            }catch(Exception e){/* No exception payloads or JVM internals are logged. */}
            finally{running=false;}
        },"s702-bounded-metrics");
        worker.setDaemon(true);worker.start();
    }
    public static String sample() {
        MemoryMXBean memory=ManagementFactory.getMemoryMXBean();MemoryUsage h=memory.getHeapMemoryUsage(),n=memory.getNonHeapMemoryUsage();
        long count=0,time=0;boolean supported=true;
        for(GarbageCollectorMXBean gc:ManagementFactory.getGarbageCollectorMXBeans()) {
            if(gc.getCollectionCount()<0||gc.getCollectionTime()<0)supported=false;
            else{count+=gc.getCollectionCount();time+=gc.getCollectionTime();}
        }
        return "{\"timestampMillis\":"+System.currentTimeMillis()+",\"jvmAvailableProcessors\":"+Runtime.getRuntime().availableProcessors()
            +",\"heapUsed\":"+h.getUsed()+",\"heapCommitted\":"+h.getCommitted()+",\"heapMax\":"+h.getMax()
            +",\"nonHeapUsed\":"+n.getUsed()+",\"nonHeapCommitted\":"+n.getCommitted()
            +",\"gcCount\":"+(supported?count:-1)+",\"gcTimeMillis\":"+(supported?time:-1)+"}";
    }
}
