using Restaurant.Api;
using Xunit;
public class WorkflowTests {
 [Fact] public void OrderRetryMatchesNormalizedPayloadButNotAnotherWaiter(){var waiter=Guid.NewGuid();var order=new Order{TableId=2,WaiterId=waiter,Instructions="No salt",Items=[new(){MenuItemId=3,Quantity=2}]};Assert.True(Workflow.SameOrder(order,new(2,[new(3,1),new(3,1)]," No salt "),waiter));Assert.False(Workflow.SameOrder(order,new(3,[new(3,2)],"No salt"),waiter));Assert.False(Workflow.SameOrder(order,new(2,[new(3,2)],"No salt"),Guid.NewGuid()));}
 [Theory]
 [InlineData("Ready","Preparing","Kitchen",false,true)]
 [InlineData("Paid","Served","Waiter",true,true)]
 [InlineData("Served","Served","Waiter",false,false)]
 [InlineData("Preparing","Ready","Kitchen",false,false)]
 [InlineData("Paid","Paid","Admin",true,false)]
 public void StatusRetriesAcknowledgeProgressWithoutMovingBackward(string current,string target,string role,bool owner,bool expected)=>Assert.Equal(expected,Workflow.IsAcknowledged(current,target,role,owner));
 [Theory]
 [InlineData("New","Preparing","Kitchen",false,true)]
 [InlineData("Preparing","Ready","Kitchen",false,true)]
 [InlineData("Ready","Served","Waiter",true,true)]
 [InlineData("Ready","Served","Waiter",false,false)]
 [InlineData("Ready","Served","Kitchen",true,false)]
 [InlineData("New","Served","Admin",true,false)]
 [InlineData("Served","Paid","Admin",true,false)]
 [InlineData("Paid","New","Admin",true,false)]
 [InlineData("New","Preparing","Cashier",true,false)]
 [InlineData("New","Preparing","Waiter",true,false)]
 [InlineData("Preparing","Ready","Admin",false,true)]
 public void EnforcesLegalTransitionsAndOwnership(string from,string to,string role,bool owner,bool expected)=>Assert.Equal(expected,Workflow.CanTransition(from,to,role,owner));

 [Fact]
 public void BillingEngineCalculatesTotalsAndTaxesWithoutRounding() {
  var orders = new List<Order> {
   new() { Status = "Served", Items = [new() { UnitPrice = 100, Quantity = 2 }] }
  };
  var settings = new PosSettings {
   ServicePercent = 5,
   TaxRulesJson = "[{\"Name\":\"GST\",\"Percent\":5}]",
   RoundingRule = "None"
  };
  var quote = BillingEngine.Calculate(orders, settings, "Percent", 10, 10, 50);
  Assert.Equal(200m, quote.Subtotal);
  Assert.Equal(20m, quote.Discount);
  Assert.Equal(9m, quote.ServiceCharge);
  Assert.Single(quote.Taxes);
  Assert.Equal(9.45m, quote.Taxes[0].Amount);
  Assert.Equal(208.45m, quote.Total);
  Assert.Equal(0m, quote.RoundingDelta);
  Assert.Equal(158.45m, quote.Outstanding);
 }

 [Fact]
 public void BillingEngineAppliesNearestWholeRoundingRuleWithDelta() {
  var orders = new List<Order> {
   new() { Status = "Served", Items = [new() { UnitPrice = 100, Quantity = 2 }] }
  };
  var settings = new PosSettings {
   ServicePercent = 5,
   TaxRulesJson = "[{\"Name\":\"GST\",\"Percent\":5}]",
   RoundingRule = "NearestWhole"
  };
  var quote = BillingEngine.Calculate(orders, settings, "Percent", 10, 10, 0);
  Assert.Equal(208m, quote.Total);
  Assert.Equal(-0.45m, quote.RoundingDelta);
  Assert.Equal(208m, quote.Outstanding);
 }

 [Fact]
 public void BillingEngineSplitEqualPreservesTotalAcrossGuests() {
  var parts = BillingEngine.SplitEqual(100m, 3);
  Assert.Equal(3, parts.Length);
  Assert.Equal(33.34m, parts[0]);
  Assert.Equal(33.33m, parts[1]);
  Assert.Equal(33.33m, parts[2]);
  Assert.Equal(100m, parts.Sum());
 }
}
