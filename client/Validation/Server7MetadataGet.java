import java.net.URI;
import java.net.http.*;
import java.io.*;
import java.time.Duration;
class Server7MetadataGet {
 public static void main(String[] args) throws Exception {
  var in=new BufferedReader(new InputStreamReader(System.in));var token=in.readLine();var path=in.readLine();
  if(!path.matches("players/me/history\\?limit=20|matches/[a-zA-Z0-9_-]+/(snapshot|replay|replay/events\\?after=[0-9]+&limit=250(&session=[A-Za-z0-9_%.-]+)?)"))throw new IllegalArgumentException();
  var c=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(15)).build();
  var r=c.send(HttpRequest.newBuilder(URI.create("https://domino-api-test.teamfho.com/api/v1/"+path)).header("Authorization","Bearer "+token).header("Content-Type","application/json").timeout(Duration.ofSeconds(40)).GET().build(),HttpResponse.BodyHandlers.ofString());
  System.out.println(r.statusCode());if(r.statusCode()==200)System.out.print(r.body());
 }
}
