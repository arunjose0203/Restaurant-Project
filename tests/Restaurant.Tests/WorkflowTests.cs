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
}
