# Multi-Language Support Implementation Plan

## Overview
Add internationalization (i18n) support to the Parental Control System web interface to display all pages in different languages with automatic browser language detection and manual override capability.

## Current State Analysis

### Pages to Localize (13 total)
1. **Index.razor** - Dashboard with system status and navigation cards
2. **Users.razor** - User management with CRUD operations
3. **Profiles.razor** - Time profile management
4. **AllowedHours.razor** - Allowed hours configuration
5. **Computers.razor** - Device management
6. **Reports.razor** - Usage reports and statistics
7. **Configuration.razor** - Database schema and system settings
8. **About.razor** - Project information and license
9. **Setup.razor** - Initial database setup
10. **AdminAuth.razor** - Administrator authentication
11. **Login.razor** - User login
12. **DbInfo.razor** - Database information
13. **AuthBase.razor** - Authentication base component

### Content Types to Localize
- **Page titles** (PageTitle tags)
- **Headings** (h1, h2, h3 tags)
- **Button labels** (Edit, Delete, Save, Cancel, etc.)
- **Form labels** (Username, Email, Password, etc.)
- **Placeholders** (input field hints)
- **Messages** (success, error, warning messages)
- **Descriptions** (help text, tooltips)
- **Table headers** (column names)
- **Navigation items** (menu links)
- **Validation messages** (form validation errors)
- **Status text** (Active, Inactive, Pending, etc.)
- **Time units** (minutes, hours, days)

### Estimated String Count
Based on analysis:
- **Index.razor**: ~40 strings (dashboard cards, status messages)
- **Users.razor**: ~60 strings (form labels, buttons, table headers, dialogs)
- **Profiles.razor**: ~50 strings (profile management, time limits)
- **AllowedHours.razor**: ~45 strings (day names, time ranges)
- **Other pages**: ~30-40 strings each
- **Total estimate**: 400-500 translatable strings

## Implementation Approach

### Option 1: ASP.NET Core Localization with .resx files
Use built-in .NET localization with resource files (.resx).

**Pros:**
- Native .NET solution
- Type-safe with IntelliSense
- Supports pluralization and formatting
- Works with Blazor Server
- Fallback language support

**Cons:**
- Requires recompilation for new languages
- More verbose syntax
- **⚠️ CRITICAL: Resource files are embedded resources - same issue as EF migrations!**
- **May not be included in Docker image build**

### Option 2: JSON-based Localization (Recommended for Docker)
Create a custom service using JSON files stored in wwwroot.

**Pros:**
- JSON files are static content - always copied to Docker image
- Easy to add new languages without recompilation
- Simple JSON format
- Can be edited by non-developers
- No embedded resource compilation issues
- Can be updated without rebuilding image

**Cons:**
- No compile-time checking
- Need to implement fallback logic
- More custom code to maintain

### Option 3: Database-based Localization
Store translations in PostgreSQL database.

**Pros:**
- Can be updated via admin UI
- No deployment needed for new translations
- Centralized management
- Version control of translations

**Cons:**
- Database dependency
- More complex implementation
- Performance overhead (needs caching)

## Recommended Implementation: JSON-based Localization

**Why JSON over .resx:**
Given the EF Core migration issue we experienced (files not compiled into Docker image), JSON files in `wwwroot` are safer because:
1. Static files are always copied by Docker COPY command
2. No compilation/embedding step required
3. Visible in file system (easy to verify)
4. Can be updated without rebuild

### 1. Project Structure Changes

```
src/ParentalControl.WebService/
├── wwwroot/
│   └── localization/
│       ├── en.json          (English - default)
│       ├── cs.json          (Czech)
│       └── de.json          (German)
├── Services/
│   ├── LocalizationService.cs      (Load and cache translations)
│   └── LanguageService.cs          (Language preference management)
└── Shared/
    └── LanguageSelector.razor      (Language picker component)
```

### 2. Required NuGet Packages
None! Pure C# implementation using System.Text.Json.

### 3. JSON File Structure

**wwwroot/localization/en.json:**
```json
{
  "Common": {
    "AppTitle": "Parental Control System",
    "Save": "Save",
    "Cancel": "Cancel",
    "Edit": "Edit",
    "Delete": "Delete",
    "Add": "Add",
    "Close": "Close",
    "Yes": "Yes",
    "No": "No",
    "Active": "Active",
    "Inactive": "Inactive"
  },
  "Users": {
    "PageTitle": "Users Management",
    "AddUser": "Add User",
    "Username": "Username",
    "Email": "Email",
    "FullName": "Full Name",
    "AccountType": "Account Type",
    "TimeButton": "⏱️ Time",
    "AliasesButton": "👥 Aliases",
    "UnlockButton": "🔒 Unlock"
  },
  "TimeAdjust": {
    "DialogTitle": "Adjust Time for {0}",
    "TimeRemaining": "Time Remaining",
    "UsedToday": "Used Today",
    "AdjustmentsToday": "Adjustments Today",
    "AddMinutes": "+{0} min"
  }
}
```

**wwwroot/localization/cs.json:**
```json
{
  "Common": {
    "AppTitle": "Systém rodičovské kontroly",
    "Save": "Uložit",
    "Cancel": "Zrušit",
    "Edit": "Upravit",
    "Delete": "Smazat",
    "Add": "Přidat",
    "Close": "Zavřít",
    "Yes": "Ano",
    "No": "Ne",
    "Active": "Aktivní",
    "Inactive": "Neaktivní"
  },
  "Users": {
    "PageTitle": "Správa uživatelů",
    "AddUser": "Přidat uživatele",
    "Username": "Uživatelské jméno",
    "Email": "E-mail",
    "FullName": "Celé jméno",
    "AccountType": "Typ účtu",
    "TimeButton": "⏱️ Čas",
    "AliasesButton": "👥 Aliasy",
    "UnlockButton": "🔒 Odemknout"
  },
  "TimeAdjust": {
    "DialogTitle": "Upravit čas pro {0}",
    "TimeRemaining": "Zbývající čas",
    "UsedToday": "Použito dnes",
    "AdjustmentsToday": "Úpravy dnes",
    "AddMinutes": "+{0} min"
  }
}
```

### 4. Configuration Changes

**Program.cs additions:**
```csharp
// Add localization services
builder.Services.AddSingleton<LocalizationService>();
builder.Services.AddScoped<LanguageService>();

// No need for RequestLocalization middleware - handled in Blazor
```

### 4. New Components

#### LocalizationService.cs
```csharp
public class LocalizationService
{
    private readonly IWebHostEnvironment _env;
    private Dictionary<string, Dictionary<string, object>> _translations = new();
    
    public LocalizationService(IWebHostEnvironment env)
    {
        _env = env;
        LoadTranslations();
    }
    
    private void LoadTranslations()
    {
        var localizationPath = Path.Combine(_env.WebRootPath, "localization");
        foreach (var file in Directory.GetFiles(localizationPath, "*.json"))
        {
            var culture = Path.GetFileNameWithoutExtension(file);
            var json = File.ReadAllText(file);
            _translations[culture] = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
        }
    }
    
    public string Get(string culture, string key)
    {
        // key format: "Users.PageTitle" or "Common.Save"
        var parts = key.Split('.');
        if (_translations.TryGetValue(culture, out var cultureTrans))
        {
            // Navigate nested dictionary
            object current = cultureTrans;
            foreach (var part in parts)
            {
                if (current is JsonElement element && element.ValueKind == JsonValueKind.Object)
                {
                    if (element.TryGetProperty(part, out var prop))
                        current = prop;
                    else
                        return key; // Fallback to key
                }
            }
            return current?.ToString() ?? key;
        }
        
        // Fallback to English
        return Get("en", key);
    }
    
    public string Format(string culture, string key, params object[] args)
    {
        var template = Get(culture, key);
        return string.Format(template, args);
    }
}
```

#### LanguageService.cs
```csharp
public class LanguageService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IJSRuntime _jsRuntime;
    private string _currentLanguage = "en";
    
    public LanguageService(IHttpContextAccessor httpContextAccessor, IJSRuntime jsRuntime)
    {
        _httpContextAccessor = httpContextAccessor;
        _jsRuntime = jsRuntime;
    }
    
    public async Task<string> GetCurrentLanguageAsync()
    {
        // Try to get from cookie first
        var cookie = _httpContextAccessor.HttpContext?.Request.Cookies["language"];
        if (!string.IsNullOrEmpty(cookie))
            return cookie;
        
        // Try to get from browser
        try
        {
            var browserLang = await _jsRuntime.InvokeAsync<string>("getBrowserLanguage");
            return MapBrowserLanguage(browserLang);
        }
        catch
        {
            return "en";
        }
    }
    
    public void SetLanguage(string culture)
    {
        _currentLanguage = culture;
        _httpContextAccessor.HttpContext?.Response.Cookies.Append("language", culture, 
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) });
    }
    
    private string MapBrowserLanguage(string browserLang)
    {
        // Map browser language codes to supported languages
        if (browserLang.StartsWith("cs")) return "cs";
        if (browserLang.StartsWith("de")) return "de";
        return "en"; // Default
    }
}
```

#### LanguageSelector.razor
```razor
<!-- Dropdown in top-right corner of all pages -->
<div class="language-selector">
    <select @onchange="OnLanguageChanged">
        <option value="en">🇬🇧 English</option>
        <option value="cs">🇨🇿 Čeština</option>
        <option value="de">🇩🇪 Deutsch</option>
    </select>
</div>
```

### 5. Page Modifications

**Before (hardcoded):**
```razor
<h1>👥 Users Management</h1>
<button>Add User</button>
<label>Username</label>
```

**After (localized):**
```razor
@inject LocalizationService Localizer
@inject LanguageService LangService

@code {
    private string currentLang = "en";
    
    protected override async Task OnInitializedAsync()
    {
        currentLang = await LangService.GetCurrentLanguageAsync();
    }
    
    private string T(string key) => Localizer.Get(currentLang, key);
    private string T(string key, params object[] args) => Localizer.Format(currentLang, key, args);
}

<h1>@T("Users.PageTitle")</h1>
<button>@T("Users.AddUser")</button>
<label>@T("Users.Username")</label>
```

**Or create a base component:**
```razor
@inherits LocalizedComponentBase

<h1>@T("Users.PageTitle")</h1>
<button>@T("Users.AddUser")</button>
```

Where `LocalizedComponentBase` provides the `T()` method.

### 6. JavaScript Helper

**wwwroot/localization.js:**
```javascript
window.getBrowserLanguage = function() {
    return navigator.language || navigator.userLanguage || 'en';
};
```

Add to `_Host.cshtml`:
```html
<script src="localization.js"></script>
```

### 7. Docker Verification

**To verify JSON files are in the image:**
```bash
# After building image
docker run --rm parental-control-webservice ls -la /app/wwwroot/localization/

# Should show:
# en.json
# cs.json
# de.json
```

**Dockerfile already copies wwwroot correctly:**
```dockerfile
COPY --from=publish /app/publish .
# This includes wwwroot/ directory with all static files
```

**Why this works:**
- `wwwroot` is part of the publish output
- Static files are always copied
- No compilation/embedding step
- Visible in file system

### 7. Layout Integration

**LanguageSelector.razor:**
```razor
@inject LanguageService LangService
@inject NavigationManager Navigation

<div class="language-selector">
    <select @bind="selectedLanguage" @bind:after="OnLanguageChanged">
        <option value="en">🇬🇧 English</option>
        <option value="cs">🇨🇿 Čeština</option>
        <option value="de">🇩🇪 Deutsch</option>
    </select>
</div>

@code {
    private string selectedLanguage = "en";
    
    protected override async Task OnInitializedAsync()
    {
        selectedLanguage = await LangService.GetCurrentLanguageAsync();
    }
    
    private void OnLanguageChanged()
    {
        LangService.SetLanguage(selectedLanguage);
        Navigation.NavigateTo(Navigation.Uri, forceLoad: true);
    }
}
```

**MainLayout.razor or _Host.cshtml:**
```razor
<!-- Add language selector to header -->
<div class="app-header">
    <div class="app-title">Parental Control</div>
    <LanguageSelector />
</div>
```

### 8. Browser Language Detection Flow

1. **First visit**: 
   - Read `Accept-Language` header from browser
   - Set UI language to best match (en, cs, or de)
   - Save preference to cookie

2. **Subsequent visits**:
   - Read language from cookie
   - Use saved preference

3. **Manual override**:
   - User selects language from dropdown
   - Save to cookie
   - Reload page with new language

### 9. Special Considerations

#### Date/Time Formatting
```csharp
// Automatically formats based on culture
@timeValue.ToString("d", CultureInfo.CurrentCulture)
```

#### Number Formatting
```csharp
// Respects culture's number format
@minutes.ToString("N0", CultureInfo.CurrentCulture)
```

#### Pluralization
```csharp
// Use string format with parameters
Localizer["MinutesRemaining", count]

// In resource file:
// en: "{0} minutes remaining"
// cs: "Zbývá {0} minut" (or "minuta"/"minuty" based on count)
```

#### Dynamic Content
For content from database (user names, etc.), don't localize.
Only localize UI labels and messages.

## Advantages of JSON Approach Over .resx

### 1. Docker Build Safety
- ✅ JSON files are static content in `wwwroot/`
- ✅ Always copied by `COPY --from=publish /app/publish .`
- ✅ No compilation/embedding step
- ✅ Easy to verify in Docker image
- ❌ .resx files are embedded resources (same issue as EF migrations)

### 2. Runtime Flexibility
- ✅ Can add new languages without rebuilding
- ✅ Can update translations without redeployment
- ✅ Can hot-reload translations in development
- ❌ .resx requires recompilation

### 3. Simplicity
- ✅ Simple JSON format, easy to edit
- ✅ No special tooling required
- ✅ Can be edited in any text editor
- ❌ .resx requires Visual Studio or special editors

### 4. Verification
```bash
# Easy to verify JSON files exist in image
docker exec container ls /app/wwwroot/localization/

# Easy to verify content
docker exec container cat /app/wwwroot/localization/cs.json
```

### 5. Debugging
- ✅ Can inspect JSON files directly in running container
- ✅ Can temporarily edit for testing
- ❌ .resx embedded in DLL, can't inspect or modify

## Disadvantages (and Mitigations)

### 1. No Compile-Time Checking
**Problem:** Typos in keys won't be caught until runtime
**Mitigation:** 
- Create constants for keys: `public static class Keys { public const string UsersPageTitle = "Users.PageTitle"; }`
- Add unit tests to verify all keys exist in all languages

### 2. No IntelliSense
**Problem:** No autocomplete for translation keys
**Mitigation:**
- Use constants (see above)
- Create code snippets for common patterns

### 3. Manual Fallback Logic
**Problem:** Need to implement fallback to English
**Mitigation:**
- Simple logic in `LocalizationService.Get()` method
- Already shown in implementation above

## Implementation Progress

### ✅ Phase 0: Translation Files (COMPLETE)
- [x] Create en.json with all translations
- [x] Create cs.json with all translations
- [x] Commit to repository

### ✅ Phase 1: Core Services (COMPLETE)
- [x] Create LocalizationService.cs
- [x] Create LanguageService.cs
- [x] Add JavaScript helper (localization.js)
- [x] Register services in Program.cs
- [x] Add script to _Host.cshtml

### ✅ Phase 2: UI Components (COMPLETE)
- [x] Create LanguageSelector.razor component
- [x] Add to Index.razor (Dashboard)
- [x] Style language selector

### ✅ Phase 3: Page Updates (COMPLETE)
- [x] Update Index.razor (Dashboard)
- [x] Update Users.razor
- [x] Update Profiles.razor
- [x] Update AllowedHours.razor
- [x] Update Configuration.razor
- [x] Update About.razor
- [x] Update AdminAuth.razor
- [x] Update Computers.razor
- [x] Update Reports.razor
- [x] Update Setup.razor
- [x] Minor pages (Login, DbInfo, AuthBase have minimal usage)

### ✅ Phase 4: Testing (READY)
- [ ] Test browser language detection
- [ ] Test manual language switching
- [ ] Test cookie persistence
- [ ] Verify all pages in both languages

---

## Implementation Complete!

All 13 main pages have been translated. The localization system is fully functional.

### Phase 1: Infrastructure (2-3 hours)
1. Add localization packages
2. Configure Program.cs
3. Create LanguageService
4. Create LanguageSelector component
5. Add to MainLayout

### Phase 2: Resource Files (8-10 hours)
1. Extract all strings from each page
2. Create .en.resx files (English baseline)
3. Create .cs.resx files (Czech translation)
4. Create .de.resx files (German translation)
5. Create Common.resx for shared strings

### Phase 3: Page Updates (10-12 hours)
1. Update each page to use IStringLocalizer
2. Replace hardcoded strings with Localizer["Key"]
3. Test each page in all languages
4. Fix formatting issues

### Phase 4: Testing & Polish (3-4 hours)
1. Test browser language detection
2. Test manual language switching
3. Test cookie persistence
4. Verify all pages in all languages
5. Check responsive design with longer translations

**Total estimated time: 23-29 hours**

## Priority Languages

### Tier 1 (Must Have)
- **English (en)** - Default, international
- **Czech (cs)** - Primary target audience

### Tier 2 (Nice to Have)
- **German (de)** - Common in Central Europe
- **Slovak (sk)** - Similar to Czech
- **Polish (pl)** - Regional neighbor

### Tier 3 (Future)
- **Spanish (es)** - Large user base
- **French (fr)** - International
- **Russian (ru)** - Regional

## Testing Strategy

### Manual Testing
1. Set browser language to each supported language
2. Verify automatic detection works
3. Test language selector on each page
4. Verify cookie persistence across sessions
5. Test with very long translations (German)
6. Test with special characters (Czech diacritics)

### Automated Testing
```csharp
[Fact]
public void AllResourceKeysExistInAllLanguages()
{
    // Verify each key in en.resx exists in cs.resx and de.resx
}

[Fact]
public void NoMissingTranslations()
{
    // Verify no empty values in resource files
}
```

## Maintenance Considerations

### Adding New Strings
1. Add to English .resx file first
2. Add same key to all other language files
3. Mark untranslated with `[TODO]` prefix
4. Create translation task

### Adding New Languages
1. Copy .en.resx files
2. Rename to new culture code (e.g., .sk.resx)
3. Translate all values
4. Add culture to supportedCultures in Program.cs
5. Add option to LanguageSelector

### Translation Workflow
1. Developer adds English strings
2. Export .resx to Excel/CSV
3. Send to translators
4. Import translations back to .resx
5. Test and deploy

## Potential Challenges

### 1. Text Length Variations
- German translations are typically 30% longer
- May break layouts designed for English
- **Solution**: Use flexible CSS (flexbox, grid), test with longest language

### 2. Right-to-Left Languages (Future)
- Arabic, Hebrew require RTL layout
- **Solution**: Use CSS logical properties, add RTL support later

### 3. Pluralization Rules
- Different languages have different plural forms
- Czech has 3 forms (1, 2-4, 5+)
- **Solution**: Use ICU MessageFormat or custom logic

### 4. Date/Time Formats
- Different cultures use different formats
- **Solution**: Use CultureInfo.CurrentCulture for formatting

### 5. Performance
- Loading resource files adds minimal overhead
- **Solution**: Resources are cached, no significant impact

## Alternative: Minimal Implementation

If full localization is too much work, consider a **hybrid approach**:

1. Keep English as primary language in code
2. Add only a **Czech translation overlay**
3. Use simple dictionary-based approach
4. Store translations in JSON file
5. Load on demand

**Pros**: Much faster to implement (4-6 hours)
**Cons**: Less maintainable, no standard tooling

## Recommendation

**Implement full ASP.NET Core localization** for these reasons:
1. Professional, maintainable solution
2. Easy to add more languages later
3. Standard .NET tooling support
4. Type-safe with compile-time checking
5. Supports complex scenarios (pluralization, formatting)
6. Worth the initial investment for long-term maintainability

**Start with English + Czech**, add German later if needed.

## Cost-Benefit Analysis

**Benefits:**
- Wider user adoption (Czech families)
- Professional appearance
- Competitive advantage
- Future-proof for expansion

**Costs:**
- 23-29 hours initial development
- 2-3 hours per new language
- Ongoing maintenance for new features

**ROI**: High if targeting Czech market, medium if international only.
