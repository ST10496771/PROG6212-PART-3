using System.Net;
using System.Net.Http.Headers;

namespace RaceDayApi.Tests;

/// <summary>Image upload endpoints (Azure Blob Storage, faked in memory for the tests).</summary>
public class BlobStorageTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public BlobStorageTests(RaceDayFactory factory)
    {
        _factory = factory;
    }

    // PNG signature followed by a few filler bytes - enough to pass the API's real-file-type check.
    private static readonly byte[] Png = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType = "image/png")
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Organiser_CanUploadBanner_AndAnyoneCanDownloadIt()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var upload = await organiser.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "banner.png"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var download = await _factory.CreateClient().GetAsync($"/api/event/{eventId}/banner");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Png, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task UploadBanner_FileThatIsNotAnImage_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var fake = System.Text.Encoding.UTF8.GetBytes("this is definitely not a png");
        var response = await organiser.PostAsync($"/api/event/{eventId}/banner", FileContent(fake, "banner.png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadBanner_DisallowedExtension_Returns400()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await organiser.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "banner.exe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var participant = await TestHelpers.CreateLoggedInClientAsync(_factory, "Participant");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await participant.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadBannerForAnotherOrganisersEvent_Returns403()
    {
        var owner = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var other = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(owner);

        var response = await other.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_UploadBanner_Returns401()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await _factory.CreateClient().PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "banner.png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetBanner_WhenNoneUploaded_Returns404()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);

        var response = await _factory.CreateClient().GetAsync($"/api/event/{eventId}/banner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_ReplacingBanner_KeepsOnlyOneImage()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var before = _factory.Storage.Count;

        await organiser.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "one.png"));
        await organiser.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "two.png"));

        Assert.Equal(before + 1, _factory.Storage.Count);
    }

    [Fact]
    public async Task Organiser_CanDeleteBanner()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        await organiser.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "banner.png"));

        var delete = await organiser.DeleteAsync($"/api/event/{eventId}/banner");
        var after = await organiser.GetAsync($"/api/event/{eventId}/banner");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
    }

    [Fact]
    public async Task DeletingAnEvent_AlsoRemovesItsBanner()
    {
        var organiser = await TestHelpers.CreateLoggedInClientAsync(_factory, "Organiser");
        var eventId = await TestHelpers.CreateEventAsync(organiser);
        var before = _factory.Storage.Count;
        await organiser.PostAsync($"/api/event/{eventId}/banner", FileContent(Png, "banner.png"));

        var response = await organiser.DeleteAsync($"/api/event/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(before, _factory.Storage.Count);
    }

    [Theory]
    [InlineData("Participant")]
    [InlineData("Organiser")]
    public async Task LoggedInUser_CanUploadViewAndDeleteOwnProfilePicture(string role)
    {
        var client = await TestHelpers.CreateLoggedInClientAsync(_factory, role);

        var before = await client.GetAsync("/api/profile/picture");
        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);

        var upload = await client.PostAsync("/api/profile/picture", FileContent(Png, "me.png"));
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var download = await client.GetAsync("/api/profile/picture");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType?.MediaType);

        var delete = await client.DeleteAsync("/api/profile/picture");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/profile/picture")).StatusCode);
    }

    [Fact]
    public async Task ProfilePicture_Anonymous_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/profile/picture", FileContent(Png, "me.png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
