using System.Text.Json;
namespace Restaurant.Api;
public static class MenuConfiguration {
 public static void Validate(MenuItem item) {
  var portions=JsonSerializer.Deserialize<List<Portion>>(item.PortionsJson)??[];
  var groups=JsonSerializer.Deserialize<List<ModifierGroup>>(item.ModifiersJson)??[];
  if(item.Stock<0||string.IsNullOrWhiteSpace(item.Station)||item.Station.Length>60||portions.Count>20||groups.Count>20||portions.Select(p=>p.Name).Distinct().Count()!=portions.Count||groups.Select(g=>g.Name).Distinct().Count()!=groups.Count||portions.Any(p=>string.IsNullOrWhiteSpace(p.Name)||p.Name.Length>60||p.Price<0||p.Price>100000)||groups.Any(g=>string.IsNullOrWhiteSpace(g.Name)||g.Name.Length>60||g.Options.Count is <1 or >30||g.Options.Select(o=>o.Name).Distinct().Count()!=g.Options.Count||g.Options.Any(o=>string.IsNullOrWhiteSpace(o.Name)||o.Name.Length>60||o.Price<0||o.Price>100000)))throw new ArgumentException("Invalid portions, modifiers, station, or stock.");
  if(item.PhotoUrl.Length>0&&(!Uri.TryCreate(item.PhotoUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"))throw new ArgumentException("Photo URL must use HTTPS.");
 }
}
