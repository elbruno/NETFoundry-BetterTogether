/*
 * Goal: Check the native provider contract and offline game loop.
 * A. Supply synthetic HTTP or explicitly scripted decisions.
 * B. Execute the same client/loop paths used by the game.
 * C. Assert legal actions, visible failures, and successful loop completion.
 */
using System.Net;
using System.Text.Json;
using ElBruno.AI.Decisions;
using ElBruno.AI.Decisions.Foundry;
using ElBruno.AI.Decisions.Ollama;
using Xunit;

namespace DecisionSpaceGame.Tests;

public sealed class ProviderTests
{
    [Theory]
    [InlineData("foundry", "/providers/microsoft/v1/systemone")]
    [InlineData("ollama", "/v1/systemone")]
    public async Task NativeHttpContractPreservesActionLabelsAndConfidence(string provider, string path)
    {
        using var handler = new FixtureHandler();
        using var http = new HttpClient(handler);
        using IDisposable disposable = provider == "foundry" ?
            new FoundryDecisionClient(http, new FoundryDecisionOptions
            {
                Endpoint = new Uri("https://fixture.invalid"), Model = "fixture-deployment",
                ApiKey = "synthetic-test-value-not-a-credential"
            }) :
            new OllamaDecisionClient(http, new OllamaDecisionOptions
            {
                Endpoint = new Uri("http://localhost:11434"), Model = "fixture-deployment"
            });
        var client = (IDecisionClient)disposable;
        var game = new Game(42);
        var result = await client.ChooseAsync(game.Snapshot(), Actions.Instructions, Actions.Options);
        Assert.Equal(path, handler.Path);
        using var request = JsonDocument.Parse(handler.Body!);
        Assert.Equal("fixture-deployment", request.RootElement.GetProperty("model").GetString());
        Assert.Equal(game.Snapshot(), request.RootElement.GetProperty("state").GetString());
        var question = request.RootElement.GetProperty("questions").GetProperty("decision");
        Assert.Equal("choice", question.GetProperty("type").GetString());
        Assert.Equal(Actions.Options.Keys.Order(), question.GetProperty("criteria").EnumerateObject().Select(p => p.Name).Order());
        Assert.False(request.RootElement.TryGetProperty("messages", out _));
        Assert.Equal("fire", result.Choice);
        Assert.Equal(0.7, result.Probabilities["fire"]);
        Assert.Equal(0.4, result.Confidence);
        Assert.True(DecisionDriver.IsLegal(result, game, out var action));
        game.Apply(action);
        Assert.Single(game.Shots);
    }

    [Theory]
    [InlineData("foundry")]
    [InlineData("ollama")]
    public async Task HttpFailuresAreNotSuccessShaped(string provider)
    {
        using var handler = new FixtureHandler(HttpStatusCode.ServiceUnavailable);
        using var http = new HttpClient(handler);
        using IDisposable disposable = provider == "foundry" ?
            new FoundryDecisionClient(http, new FoundryDecisionOptions
            {
                Endpoint = new Uri("https://fixture.invalid"), Model = "fixture",
                ApiKey = "synthetic-test-value-not-a-credential"
            }) :
            new OllamaDecisionClient(http, new OllamaDecisionOptions { Model = "fixture" });
        var attempt = await DecisionCalls.ChooseAsync((IDecisionClient)disposable, new Game(42).Snapshot(), default);
        Assert.Null(attempt.Decision);
        Assert.Contains("503", attempt.Error);
    }

    [Fact]
    public async Task ReplayIsExplicitAndNeverUsesHttp()
    {
        var replay = new ReplayClient();
        var result = await replay.ChooseAsync("", "", Actions.Options);
        Assert.Equal("fire", result.Choice);
        Assert.Equal(1, result.Probabilities["fire"]);
    }

    [Fact]
    public async Task ConsoleLoopCanRunWithoutKeyboardUsingExplicitOfflineClient()
    {
        int exitCode = await GameConsole.RunAsync(new ReplayClient(), headless: true, seconds: 1);
        Assert.Equal(0, exitCode);
    }

    private sealed class FixtureHandler(HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
    {
        public string? Path { get; private set; }
        public string? Body { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(token);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent("""
                    {"answers":{"decision":{"type":"choice","choice":"fire",
                    "probabilities":{"move_left":0.1,"move_right":0.1,"fire":0.7,"stop":0.1},"confidence":0.4}}}
                    """)
            };
        }
    }
}
