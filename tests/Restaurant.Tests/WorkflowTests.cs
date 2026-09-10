using Restaurant.Api;
using Xunit;
public class WorkflowTests {
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
