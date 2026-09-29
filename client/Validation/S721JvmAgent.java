import java.lang.instrument.Instrumentation;
import java.lang.management.ManagementFactory;
import java.nio.file.*;
import java.nio.file.attribute.PosixFilePermissions;
public final class S721JvmAgent {
 public static void agentmain(String option, Instrumentation ignored) throws Exception {
  if(!option.equals("/tmp/domino-s721-jvm.json"))throw new IllegalArgumentException();
  Path output=Path.of(option);Files.createFile(output,PosixFilePermissions.asFileAttribute(PosixFilePermissions.fromString("rw-------")));
  Thread t=new Thread(()->{try {
   for(int i=0;i<650&&Files.exists(output);i++) {
    var h=ManagementFactory.getMemoryMXBean().getHeapMemoryUsage();var n=ManagementFactory.getMemoryMXBean().getNonHeapMemoryUsage();
    long count=0,ms=0;for(var g:ManagementFactory.getGarbageCollectorMXBeans()){count+=Math.max(0,g.getCollectionCount());ms+=Math.max(0,g.getCollectionTime());}
    String s="{\"timestampMillis\":"+System.currentTimeMillis()+",\"jvmAvailableProcessors\":"+Runtime.getRuntime().availableProcessors()+",\"heapUsed\":"+h.getUsed()+",\"heapCommitted\":"+h.getCommitted()+",\"heapMax\":"+h.getMax()+",\"nonHeapUsed\":"+n.getUsed()+",\"nonHeapCommitted\":"+n.getCommitted()+",\"gcCount\":"+count+",\"gcTimeMillis\":"+ms+",\"threadCount\":"+ManagementFactory.getThreadMXBean().getThreadCount()+"}";
    Files.writeString(output,s);Thread.sleep(2000);
   }
  }catch(Exception ignoredError){}} ,"s721-bounded-metrics");t.setDaemon(true);t.start();
 }
}
