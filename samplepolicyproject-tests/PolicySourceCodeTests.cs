using System;
using Xunit;
using SamplePolicyProject;

namespace samplepolicyproject_tests
{
    public class PolicySourceCodeTests
    {
        [Fact]
        public void GenerateCorrelationIdTest()
        {
            var correlationId = PolicySourceCode.GenerateCorrelationId();
            Assert.NotNull(correlationId);

            bool isValidGuid = Guid.TryParse(correlationId, out Guid correlationIdGuid);
            Assert.True(isValidGuid);
        }
    }
}
