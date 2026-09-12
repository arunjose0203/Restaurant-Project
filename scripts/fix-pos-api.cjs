const fs=require('node:fs');
for(const file of ['PosEndpoints.cs','GuestEndpoints.cs','IdentityEndpoints.cs','IntegrationEndpoints.cs']){const p='server/Restaurant.Api/'+file;let s=fs.readFileSync(p,'utf8').replaceAll('Results.Ok();','Results.Ok(new {ok=true});');fs.writeFileSync(p,s);}
let p='server/Restaurant.Api/Program.cs';let s=fs.readFileSync(p,'utf8');
s=s.replace('await db.Users.Select(x=>new{x.Id,x.Name,x.Email,x.Role,x.Active}).ToListAsync()', 'await db.Users.Where(x=>db.StaffBranches.Any(m=>m.UserId==x.Id&&m.BranchId==db.CurrentBranchId)).Select(x=>new{x.Id,x.Name,x.Email,Role=db.StaffBranches.Where(m=>m.UserId==x.Id&&m.BranchId==db.CurrentBranchId).Select(m=>m.Role).First(),x.Active}).ToListAsync()');
s=s.replace('if(string.IsNullOrWhiteSpace(r.Name)||!r.Email.Contains', 'if(id!=Guid.Empty&&!await db.StaffBranches.AnyAsync(m=>m.UserId==id&&m.BranchId==db.CurrentBranchId))return Results.NotFound();if(id!=Guid.Empty&&await db.Users.AnyAsync(u=>u.Id==id&&u.Owner)&&id!=UserId(actor))return Results.Forbid();if(string.IsNullOrWhiteSpace(r.Name)||!r.Email.Contains');
s=s.replace('total=orders.Sum(x=>x.Items.Sum(i=>i.Quantity*i.UnitPrice))','total=(await PosService.Quote(db,s)).Total');
s=s.replace('orders.All(x=>x.Status=="Served")','orders.All(x=>x.Status=="Served"||x.Status=="Voided")');
fs.writeFileSync(p,s);
