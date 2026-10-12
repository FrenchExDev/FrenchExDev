$TraefiDockerComposeCommandsSymbols = @{
        LogLevel = "log.level"
        Api = @{
            Dashboard = "api.dashboard"
            Insecure = "api.insecure"
        }
        Providers = @{
            Docker = @{
                _ = "providers.docker"
                ExposedByDefault = "providers.docker.exposedbydefault"
            }
        }
        Ping = "ping"
    }
