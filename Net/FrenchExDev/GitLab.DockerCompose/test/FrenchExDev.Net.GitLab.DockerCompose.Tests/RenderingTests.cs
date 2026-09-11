using FrenchExDev.Net.GitLab.DockerCompose;
using FrenchExDev.Net.GitLab.DockerCompose.Rendering;

namespace FrenchExDev.Net.GitLab.DockerCompose.Tests;

public class RenderingTests
{
    [Fact]
    public void Render_MinimalConfig_EmitsExternalUrl()
    {
        var config = new GitLabOmnibusConfig
        {
            ExternalUrl = "https://gitlab.example.com"
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("external_url 'https://gitlab.example.com'");
    }

    [Fact]
    public void Render_NullConfig_EmitsEmpty()
    {
        var config = new GitLabOmnibusConfig();
        var ruby = GitLabRbRenderer.Render(config);
        ruby.Trim().ShouldBeEmpty();
    }

    [Fact]
    public void Render_NginxSettings_EmitsCorrectRuby()
    {
        var config = new GitLabOmnibusConfig
        {
            ExternalUrl = "https://gitlab.example.com",
            Nginx = new NginxConfig
            {
                Enable = true,
                ClientMaxBodySize = "0",
                RedirectHttpToHttps = false,
                Http2Enabled = true,
                ProxySetHeaders = new Dictionary<string, string?>
                {
                    ["X-Forwarded-Proto"] = "https",
                    ["X-Forwarded-Ssl"] = "on",
                },
            },
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("external_url 'https://gitlab.example.com'");
        ruby.ShouldContain("nginx['enable'] = true");
        ruby.ShouldContain("nginx['client_max_body_size'] = \"0\"");
        ruby.ShouldContain("nginx['redirect_http_to_https'] = false");
        ruby.ShouldContain("nginx['http2_enabled'] = true");
        ruby.ShouldContain("nginx['proxy_set_headers'] = {");
        ruby.ShouldContain("\"X-Forwarded-Proto\" => \"https\"");
    }

    [Fact]
    public void Render_GitLabRails_EmitsSmtpSettings()
    {
        var config = new GitLabOmnibusConfig
        {
            GitlabRails = new GitLabRailsConfig
            {
                SmtpEnable = true,
                SmtpAddress = "smtp.example.com",
            },
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("gitlab_rails['smtp_enable'] = true");
        ruby.ShouldContain("gitlab_rails['smtp_address'] = \"smtp.example.com\"");
    }

    [Fact]
    public void Render_Boolean_LowercaseRuby()
    {
        var config = new GitLabOmnibusConfig
        {
            Nginx = new NginxConfig { RedirectHttpToHttps = true },
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("= true");
        ruby.ShouldNotContain("= True", Case.Sensitive);
    }

    [Fact]
    public void Render_OnlyNonNullPropertiesEmitted()
    {
        var config = new GitLabOmnibusConfig
        {
            Nginx = new NginxConfig { Enable = true },
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("nginx['enable'] = true");
        // Other nginx properties (which are null) should not appear
        var lines = ruby.Split('\n').Where(l => l.Contains("nginx[")).ToList();
        lines.Count.ShouldBe(1);
    }

    [Fact]
    public void Render_MultipleStandaloneUrls()
    {
        var config = new GitLabOmnibusConfig
        {
            ExternalUrl = "https://gitlab.example.com",
            RegistryExternalUrl = "https://registry.example.com",
            PagesExternalUrl = "https://pages.example.com",
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("external_url 'https://gitlab.example.com'");
        ruby.ShouldContain("registry_external_url 'https://registry.example.com'");
        ruby.ShouldContain("pages_external_url 'https://pages.example.com'");
    }

    [Fact]
    public void Render_LetsEncrypt_EmitsSettings()
    {
        var config = new GitLabOmnibusConfig
        {
            Letsencrypt = new LetsencryptConfig
            {
                AutoRenew = true,
            },
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("letsencrypt['auto_renew'] = true");
    }

    [Fact]
    public void Render_Redis_EmitsSettings()
    {
        var config = new GitLabOmnibusConfig
        {
            Redis = new RedisConfig
            {
                Enable = true,
                Maxmemory = "2gb",
            },
        };

        var ruby = GitLabRbRenderer.Render(config);

        ruby.ShouldContain("redis['enable'] = true");
        ruby.ShouldContain("redis['maxmemory'] = \"2gb\"");
    }
}
