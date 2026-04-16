using GroundControl.Api.Client;
using GroundControl.Api.Client.Contracts;
using GroundControl.E2E.Tests.Infrastructure;
using Shouldly;

namespace GroundControl.E2E.Tests.Scenarios;

/// <summary>
/// End-to-end workflow verifying that the API refuses to delete a template that is
/// associated with at least one project until the association is removed.
/// </summary>
public sealed class CascadingDeletionPrevention : EndToEndTestBase
{
    private const string TemplateIdKey = "TemplateId";
    private const string ProjectIdKey = "ProjectId";

    public CascadingDeletionPrevention(AspireFixture fixture)
        : base(fixture) { }

    [Fact, Step(1)]
    public Task Step01_CreateTemplate() => RunStep(1, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "template", "create",
            "--name", "Shared Base Template");

        // Assert
        result.ShouldSucceed();
        var template = result.ParseOutput<TemplateResponse>();
        template.Id.ShouldNotBe(Guid.Empty);
        template.Name.ShouldBe("Shared Base Template");

        Set(TemplateIdKey, template.Id);
    });

    [Fact, Step(2)]
    public Task Step02_CreateProject() => RunStep(2, async () =>
    {
        // Arrange & Act
        var result = await Cli.RunAsync(TestCancellationToken,
            "project", "create",
            "--name", "Template Consumer Project");

        // Assert
        result.ShouldSucceed();
        var project = result.ParseOutput<ProjectResponse>();
        project.Id.ShouldNotBe(Guid.Empty);

        Set(ProjectIdKey, project.Id);
    });

    [Fact, Step(3)]
    public Task Step03_AssociateTemplate() => RunStep(3, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var templateId = Get<Guid>(TemplateIdKey);

        // Act
        var updatedProject = await ApiClient.AddProjectTemplateHandlerAsync(
            projectId, templateId, TestCancellationToken);

        // Assert
        updatedProject.ShouldNotBeNull();
        updatedProject.TemplateIds.ShouldContain(templateId);
    });

    [Fact, Step(4)]
    public Task Step04_DeleteAssociatedTemplateFails() => RunStep(4, async () =>
    {
        // Arrange
        var templateId = Get<Guid>(TemplateIdKey);
        var template = await ApiClient.GetTemplateHandlerAsync(templateId, TestCancellationToken);

        // Act
        GroundControlClient.SetIfMatch(template.Version);
        var ex = await Should.ThrowAsync<GroundControlApiClientException>(async () =>
            await ApiClient.DeleteTemplateHandlerAsync(templateId, TestCancellationToken));

        // Assert
        ex.StatusCode.ShouldBe(409);
    });

    [Fact, Step(5)]
    public Task Step05_RemoveAssociation() => RunStep(5, async () =>
    {
        // Arrange
        var projectId = Get<Guid>(ProjectIdKey);
        var templateId = Get<Guid>(TemplateIdKey);

        // Act
        var updatedProject = await ApiClient.RemoveProjectTemplateHandlerAsync(
            projectId, templateId, TestCancellationToken);

        // Assert
        updatedProject.ShouldNotBeNull();
        updatedProject.TemplateIds.ShouldNotContain(templateId);
    });

    [Fact, Step(6)]
    public Task Step06_DeleteTemplateSucceeds() => RunStep(6, async () =>
    {
        // Arrange
        var templateId = Get<Guid>(TemplateIdKey);
        var template = await ApiClient.GetTemplateHandlerAsync(templateId, TestCancellationToken);

        // Act
        GroundControlClient.SetIfMatch(template.Version);
        await ApiClient.DeleteTemplateHandlerAsync(templateId, TestCancellationToken);

        // Assert (no exception)
    });

    [Fact, Step(7)]
    public Task Step07_GetDeletedTemplateReturns404() => RunStep(7, async () =>
    {
        // Arrange
        var templateId = Get<Guid>(TemplateIdKey);

        // Act
        var ex = await Should.ThrowAsync<GroundControlApiClientException>(async () =>
            await ApiClient.GetTemplateHandlerAsync(templateId, TestCancellationToken));

        // Assert
        ex.StatusCode.ShouldBe(404);
    });
}