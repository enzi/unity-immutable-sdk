# Fork to enable Linux support for Immutable Passport

Since the outdated Voltstro package got removed, add it back:

Add to manifest.json `scopedRegistries`
```
{
      "name": "Voltstro UPM",
      "url": "https://upm-pkgs.voltstro.dev",
      "scopes": [
        "dev.voltstro",
        "org.nuget"
      ],
      "overrideBuiltIns": true
    }
```

Add at least the cef linux runtime to dependencies:
- `"dev.voltstro.unitywebbrowser.engine.cef.linux.x64": "2.2.6",`
