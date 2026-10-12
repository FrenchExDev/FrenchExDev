using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FrenchExDev.Net.Injectable.SourceGenerator.Lib;

public static class InjectableEmitter
{
    // ── Microsoft.Extensions.DependencyInjection ────────────────────

    public static string EmitMicrosoft(InjectableEmitModel model)
    {
        var sb = new StringBuilder(2048);
        var name = SanitizeName(model.AssemblyName);

        EmitHeader(sb);
        EmitClassOpen(sb, name, "Microsoft.Extensions.DependencyInjection",
            "global::Microsoft.Extensions.DependencyInjection.IServiceCollection", "services");

        EmitMicrosoftBody(sb, model);

        EmitClassClose(sb, "services");
        return sb.ToString();
    }

    private static void EmitMicrosoftBody(StringBuilder sb, InjectableEmitModel model)
    {
        foreach (var service in model.Services.Where(s => s.Key is null))
            EmitMicrosoftService(sb, service);

        EmitMicrosoftKeyedBlock(sb, model.Services.Where(s => s.Key is not null).ToList());

        if (model.Decorators.Count > 0)
        {
            sb.AppendLine();
            EmitMicrosoftDecorators(sb, model.Decorators);
        }
    }

    private static void EmitMicrosoftService(StringBuilder sb, InjectableServiceModel service)
    {
        var prefix = service.TryAdd ? "TryAdd" : "Add";

        if (service.IsOpenGeneric)
        {
            EmitMicrosoftOpenGeneric(sb, service, prefix);
            return;
        }

        EmitRegistrationLines(sb, "services", prefix, service.Scope,
            service.ServiceTypesFull, service.ImplementationTypeFull);
    }

    private static void EmitMicrosoftOpenGeneric(StringBuilder sb, InjectableServiceModel service, string prefix)
    {
        if (service.ServiceTypesFull.Count == 0)
        {
            sb.AppendLine($"        services.{prefix}{service.Scope}(typeof({service.ImplementationTypeFull}));");
        }
        else
        {
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        services.{prefix}{service.Scope}(typeof({serviceType}), typeof({service.ImplementationTypeFull}));");
        }
    }

    private static void EmitMicrosoftKeyedBlock(StringBuilder sb, List<InjectableServiceModel> keyedServices)
    {
        if (keyedServices.Count == 0)
            return;

        sb.AppendLine();
        sb.AppendLine("#if NET8_0_OR_GREATER");
        foreach (var service in keyedServices)
            EmitMicrosoftKeyedService(sb, service);
        sb.AppendLine("#endif");
    }

    private static void EmitMicrosoftKeyedService(StringBuilder sb, InjectableServiceModel service)
    {
        var prefix = service.TryAdd ? "TryAddKeyed" : "AddKeyed";
        var key = EscapeString(service.Key!);

        if (service.IsOpenGeneric)
        {
            EmitMicrosoftKeyedOpenGeneric(sb, service, prefix, key);
            return;
        }

        if (service.ServiceTypesFull.Count == 0)
            sb.AppendLine($"        services.{prefix}{service.Scope}<{service.ImplementationTypeFull}>(\"{key}\");");
        else
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        services.{prefix}{service.Scope}<{serviceType}, {service.ImplementationTypeFull}>(\"{key}\");");
    }

    private static void EmitMicrosoftKeyedOpenGeneric(StringBuilder sb, InjectableServiceModel service, string prefix, string key)
    {
        if (service.ServiceTypesFull.Count == 0)
            sb.AppendLine($"        services.{prefix}{service.Scope}(typeof({service.ImplementationTypeFull}), \"{key}\");");
        else
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        services.{prefix}{service.Scope}(typeof({serviceType}), \"{key}\", typeof({service.ImplementationTypeFull}));");
    }

    private static void EmitMicrosoftDecorators(StringBuilder sb, IReadOnlyList<InjectableDecoratorModel> decorators)
    {
        foreach (var d in decorators.OrderBy(x => x.Order))
            EmitMicrosoftDecorator(sb, d);
    }

    private static void EmitMicrosoftDecorator(StringBuilder sb, InjectableDecoratorModel d)
    {
        sb.AppendLine("        {");
        sb.AppendLine($"            var inner = services.LastOrDefault(d => d.ServiceType == typeof({d.ServiceTypeFull}));");
        sb.AppendLine("            if (inner is not null)");
        sb.AppendLine("            {");
        sb.AppendLine("                services.Remove(inner);");
        sb.AppendLine($"                services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor(");
        sb.AppendLine($"                    typeof({d.ServiceTypeFull}),");
        sb.AppendLine($"                    sp => global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance<{d.DecoratorTypeFull}>(");
        sb.AppendLine($"                        sp,");
        sb.AppendLine($"                        inner.ImplementationFactory is not null");
        sb.AppendLine($"                            ? inner.ImplementationFactory(sp)");
        sb.AppendLine($"                            : global::Microsoft.Extensions.DependencyInjection.ActivatorUtilities.CreateInstance(sp, inner.ImplementationType!)),");
        sb.AppendLine("                    inner.Lifetime));");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    // ── Simple Injector ──────────────────────────────────────────────

    public static string EmitSimpleInjector(InjectableEmitModel model)
    {
        var sb = new StringBuilder(2048);
        var name = SanitizeName(model.AssemblyName);

        EmitHeader(sb);
        EmitClassOpen(sb, name, "SimpleInjector", "global::SimpleInjector.Container", "container");

        EmitSimpleInjectorBody(sb, model);

        EmitClassClose(sb, "container");
        return sb.ToString();
    }

    private static void EmitSimpleInjectorBody(StringBuilder sb, InjectableEmitModel model)
    {
        foreach (var service in model.Services)
            EmitSimpleInjectorService(sb, service);

        if (model.Decorators.Count > 0)
        {
            sb.AppendLine();
            foreach (var d in model.Decorators.OrderBy(x => x.Order))
                sb.AppendLine($"        container.RegisterDecorator<{d.ServiceTypeFull}, {d.DecoratorTypeFull}>();");
        }
    }

    private static void EmitSimpleInjectorService(StringBuilder sb, InjectableServiceModel service)
    {
        var lifestyle = ScopeToLifestyle(service.Scope);

        if (service.IsOpenGeneric)
        {
            EmitSimpleInjectorOpenGeneric(sb, service, lifestyle);
            return;
        }

        if (service.ServiceTypesFull.Count == 0)
            sb.AppendLine($"        container.Register<{service.ImplementationTypeFull}>({lifestyle});");
        else
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        container.Register<{serviceType}, {service.ImplementationTypeFull}>({lifestyle});");
    }

    private static void EmitSimpleInjectorOpenGeneric(StringBuilder sb, InjectableServiceModel service, string lifestyle)
    {
        if (service.ServiceTypesFull.Count == 0)
            sb.AppendLine($"        container.Register(typeof({service.ImplementationTypeFull}), {lifestyle});");
        else
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        container.Register(typeof({serviceType}), typeof({service.ImplementationTypeFull}), {lifestyle});");
    }

    private static string ScopeToLifestyle(string scope)
    {
        switch (scope)
        {
            case "Singleton": return "global::SimpleInjector.Lifestyle.Singleton";
            case "Scoped": return "global::SimpleInjector.Lifestyle.Scoped";
            case "Transient": return "global::SimpleInjector.Lifestyle.Transient";
            default: return "global::SimpleInjector.Lifestyle.Transient";
        }
    }

    // ── DryIoc ──────────────────────────────────────────────────────

    public static string EmitDryIoc(InjectableEmitModel model)
    {
        var sb = new StringBuilder(2048);
        var name = SanitizeName(model.AssemblyName);

        EmitHeader(sb);
        EmitClassOpen(sb, name, "DryIoc", "global::DryIoc.IContainer", "container");

        EmitDryIocBody(sb, model);

        EmitClassClose(sb, "container");
        return sb.ToString();
    }

    private static void EmitDryIocBody(StringBuilder sb, InjectableEmitModel model)
    {
        foreach (var service in model.Services.Where(s => s.Key is null))
            EmitDryIocService(sb, service);

        var keyedServices = model.Services.Where(s => s.Key is not null).ToList();
        if (keyedServices.Count > 0)
        {
            sb.AppendLine();
            foreach (var service in keyedServices)
                EmitDryIocKeyedService(sb, service);
        }

        if (model.Decorators.Count > 0)
        {
            sb.AppendLine();
            foreach (var d in model.Decorators.OrderBy(x => x.Order))
                sb.AppendLine($"        container.Register<{d.ServiceTypeFull}, {d.DecoratorTypeFull}>(setup: global::DryIoc.Setup.Decorator);");
        }
    }

    private static void EmitDryIocService(StringBuilder sb, InjectableServiceModel service)
    {
        var reuse = ScopeToReuse(service.Scope);
        var ifAlready = service.TryAdd ? ", ifAlreadyRegistered: global::DryIoc.IfAlreadyRegistered.Keep" : "";

        if (service.IsOpenGeneric)
        {
            EmitDryIocOpenGeneric(sb, service, reuse, ifAlready);
            return;
        }

        if (service.ServiceTypesFull.Count == 0)
            sb.AppendLine($"        container.Register<{service.ImplementationTypeFull}>({reuse}{ifAlready});");
        else
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        container.Register<{serviceType}, {service.ImplementationTypeFull}>({reuse}{ifAlready});");
    }

    private static void EmitDryIocOpenGeneric(StringBuilder sb, InjectableServiceModel service, string reuse, string ifAlready)
    {
        if (service.ServiceTypesFull.Count == 0)
            sb.AppendLine($"        container.Register(typeof({service.ImplementationTypeFull}), reuse: {reuse}{ifAlready});");
        else
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        container.Register(typeof({serviceType}), typeof({service.ImplementationTypeFull}), reuse: {reuse}{ifAlready});");
    }

    private static void EmitDryIocKeyedService(StringBuilder sb, InjectableServiceModel service)
    {
        var reuse = ScopeToReuse(service.Scope);
        var key = EscapeString(service.Key!);
        var ifAlready = service.TryAdd ? ", ifAlreadyRegistered: global::DryIoc.IfAlreadyRegistered.Keep" : "";

        if (service.ServiceTypesFull.Count == 0)
            sb.AppendLine($"        container.Register<{service.ImplementationTypeFull}>({reuse}, serviceKey: \"{key}\"{ifAlready});");
        else
            foreach (var serviceType in service.ServiceTypesFull)
                sb.AppendLine($"        container.Register<{serviceType}, {service.ImplementationTypeFull}>({reuse}, serviceKey: \"{key}\"{ifAlready});");
    }

    // ── Shared structure helpers ────────────────────────────────────

    private static void EmitHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }

    private static void EmitClassOpen(StringBuilder sb, string name, string ns, string paramType, string paramName)
    {
        sb.AppendLine($"namespace {ns};");
        sb.AppendLine();
        sb.AppendLine($"public static class {name}InjectableExtensions");
        sb.AppendLine("{");
        sb.AppendLine($"    public static {paramType} Add{name}Injectables(");
        sb.AppendLine($"        this {paramType} {paramName})");
        sb.AppendLine("    {");
    }

    private static void EmitClassClose(StringBuilder sb, string paramName)
    {
        sb.AppendLine($"        return {paramName};");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static void EmitRegistrationLines(StringBuilder sb, string param, string prefix, string scope,
        IReadOnlyList<string> serviceTypes, string implType)
    {
        if (serviceTypes.Count == 0)
            sb.AppendLine($"        {param}.{prefix}{scope}<{implType}>();");
        else
            foreach (var serviceType in serviceTypes)
                sb.AppendLine($"        {param}.{prefix}{scope}<{serviceType}, {implType}>();");
    }

    private static string ScopeToReuse(string scope)
    {
        switch (scope)
        {
            case "Singleton": return "global::DryIoc.Reuse.Singleton";
            case "Scoped": return "global::DryIoc.Reuse.ScopedOrSingleton";
            case "Transient": return "global::DryIoc.Reuse.Transient";
            default: return "global::DryIoc.Reuse.Transient";
        }
    }

    public static string SanitizeName(string assemblyName)
    {
        var sb = new StringBuilder(assemblyName.Length);
        var capitalizeNext = true;
        foreach (var c in assemblyName)
        {
            if (c == '.' || c == '-' || c == '_' || c == ' ')
            {
                capitalizeNext = true;
                continue;
            }

            if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string EscapeString(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
