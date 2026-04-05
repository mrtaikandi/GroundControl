using System.Reflection;
using Xunit.v3;

namespace GroundControl.E2E.Tests.Infrastructure;

/// <summary>
/// Orders test cases within a class by their <see cref="StepAttribute.Order"/> value.
/// Tests without <see cref="StepAttribute"/> are placed at the end.
/// </summary>
public sealed class StepOrderer : ITestCaseOrderer
{
    IReadOnlyCollection<TTestCase> ITestCaseOrderer.OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> testCases)
    {
        return testCases
            .OrderBy(tc =>
            {
                var testMethod = tc.TestMethod;
                if (testMethod is null)
                {
                    return int.MaxValue;
                }

                // Get the method from the test method's type property
                var methodProp = testMethod.GetType().GetProperty("Method", BindingFlags.Instance | BindingFlags.Public);
                if (methodProp?.GetValue(testMethod) is not MethodInfo method)
                {
                    return int.MaxValue;
                }

                var stepAttribute = method.GetCustomAttribute<StepAttribute>();
                return stepAttribute?.Order ?? int.MaxValue;
            })
            .ToList();
    }
}