using NUnit.Framework;
using VikunjaPlugin.ConfigFlow;

namespace VikunjaPlugin.Tests;

[TestFixture]
public sealed class ConfigFlowTests
{
	[TestCase("https://vikunja.example.com", "https://vikunja.example.com/api/v1/")]
	[TestCase("https://vikunja.example.com/", "https://vikunja.example.com/api/v1/")]
	[TestCase("https://vikunja.example.com/api/v1", "https://vikunja.example.com/api/v1/")]
	[TestCase("  http://192.168.1.5:3456/api/v1/  ", "http://192.168.1.5:3456/api/v1/")]
	public void The_instance_address_becomes_the_api_root(string input, string expected)
	{
		Assert.That(VikunjaConfigFlow.ToApiAddress(input)?.ToString(), Is.EqualTo(expected));
	}

	[TestCase("")]
	[TestCase("not a url")]
	[TestCase("ftp://vikunja.example.com")]
	public void An_address_that_is_not_http_is_refused(string input)
	{
		Assert.That(VikunjaConfigFlow.ToApiAddress(input), Is.Null);
	}
}
