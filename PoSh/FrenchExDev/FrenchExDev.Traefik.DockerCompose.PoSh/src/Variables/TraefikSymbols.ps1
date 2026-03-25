$TraefikSymbols = @{
    Keys = @{
        Rule        = "rule"
        Service     = "service"
        Entrypoints = "entrypoints"
        Tls         = "tls"
        LoadBalancer = @{
            Server = @{
                Port = "loadbalancer.server.port"
            }
            PassHostHeader = "loadbalancer.passhostheader"
        }
        Redirect = @{
            Scheme = "redirectscheme.scheme"
            Port = "redirectscheme.port"
            Permanent = "redirectscheme.permanent"
        }
    }
}
