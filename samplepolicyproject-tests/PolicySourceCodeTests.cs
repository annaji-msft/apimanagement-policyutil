using System;
using Xunit;
using SamplePolicyProject;
using PolicyLib;
using Moq;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace samplepolicyproject_tests
{
    public class PolicySourceCodeTests
    {
        [Fact]
        public void GenerateCorrelationIdTest()
        {
            var correlationId = PolicySourceCode.GenerateCorrelationId();
            bool isValidGuid = Guid.TryParse(correlationId, out Guid correlationIdGuid);

            Assert.NotNull(correlationId);
            Assert.True(isValidGuid);
        }

        [Fact]
        public void CheckForToken_ShouldPass_Test()
        {
            PolicySourceCode.context = MockContextVariable();
            var checkToken = PolicySourceCode.CheckForToken();
            Assert.True(checkToken);
        }

        [Fact]
        public void CheckForToken_ShouldFail_Test()
        {
            PolicySourceCode.context = MockContextVariable(@"{ Token: { AccessToken: '' } }");
            var checkToken = PolicySourceCode.CheckForToken();
            Assert.False(checkToken);
        }

        [Fact]
        public void GetAuthHeaderValueTest()
        {
            PolicySourceCode.context = MockContextVariable();
            var authHeaderValue = PolicySourceCode.GetAuthHeaderValue();

            Assert.NotNull(authHeaderValue);
            Assert.Equal(" Bearer TestToken", authHeaderValue);
        }

        public static IProxyRequestContext MockContextVariable(string jsonStr = @"{ Token: { AccessToken: 'TestToken' } }")
        {
            var contextMock = new Mock<IProxyRequestContext>();
            var variables = new Dictionary<string, object>();
            var jsonObject = JObject.Parse(jsonStr);
            variables.Add("tokens", jsonObject);

            contextMock
                .SetupGet((context) => context.Variables)
                .Returns(variables);

            return contextMock.Object;
        }
    }
}
