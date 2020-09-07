using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TechTalk.SpecFlow;

namespace DbUpdatePortal.TestAutomation.Steps
{
    [Binding]
    public class WorkFlowPageSteps
    {
        private readonly WorkFlowPage _workFlowPage;

        public WorkFlowPageSteps(ScenarioContext scenarioContext)
        {
            _workFlowPage = WorkFlowPage;
        }

        [Given(@"I navigate to the Db Update Workflow page")]
        public void GivenINavigateToTheDbUpdateWorkflowPage()
        {
            _workFlowPage.NavigateTo();
        }

        [Then(@"The Db Update Workflow page has loaded")]
        public void ThenTheDbUpdateWorkflowPageHasLoaded()
        {
            Assert.IsTrue(_workFlowPage.HasLoaded);
        }

    }
}
