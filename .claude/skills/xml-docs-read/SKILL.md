---
name: xml-docs-read
description: Query and read XML documentation files shipped with .NET NuGet packages. Use when you want to look up API summaries, parameter descriptions, or remarks for third-party libraries without decompiling.
allowed-tools: Bash, Grep
---

# .NET XML Documentation Files

NuGet packages commonly ship an `.xml` file alongside the `.dll` (same name, same folder). This is the standard
.NET XML doc format — the same file IDEs use for IntelliSense tooltips.

## Location

```
~/.nuget/packages/<package-name>/<version>/lib/<tfm>/<AssemblyName>.xml
```

Example:

```
~/.nuget/packages/nats.client.keyvaluestore/2.7.1/lib/net8.0/NATS.Client.KeyValueStore.xml
```

## Schema

```xml
<doc>
  <assembly><name>NATS.Client.KeyValueStore</name></assembly>
  <members>
    <member name="T:Namespace.ClassName">         <!-- type      -->
    <member name="M:Namespace.ClassName.Method">  <!-- method    -->
    <member name="P:Namespace.ClassName.Prop">    <!-- property  -->
    <member name="F:Namespace.ClassName.Field">   <!-- field     -->
    <member name="E:Namespace.ClassName.Event">   <!-- event     -->
  </members>
</doc>
```

Each `<member>` may contain: `<summary>`, `<param name="...">`, `<typeparam name="...">`,
`<returns>`, `<remarks>`, `<exception cref="...">`.

Member name prefixes: `T:` type, `M:` method, `P:` property, `F:` field, `E:` event.

## Querying

### Grep — fast text search (works everywhere)

```bash
# Find a method entry and show its doc block
grep -A 10 'name="M:.*UpdateAsync' MyLib.xml

# List all type entries
grep 'name="T:' MyLib.xml
```

### PowerShell — Select-Xml with XPath (structured, cross-platform via `pwsh`)

```powershell
# All type summaries
Select-Xml -Path MyLib.xml -XPath "//member[starts-with(@name,'T:')]" |
    ForEach-Object { $_.Node.OuterXml }

# Docs for members matching a name pattern
Select-Xml -Path MyLib.xml -XPath "//member[contains(@name,'UpdateAsync')]" |
    ForEach-Object { $_.Node.OuterXml }

# Dot-notation access via [xml] cast
$doc = [xml](Get-Content MyLib.xml)
$doc.doc.members.member | Where-Object { $_.name -like 'M:*Update*' } | Format-List
```

> Use Grep for quick lookups; use `Select-Xml` when you need XPath filters or structured output.

## Core workflow

1. Locate the `.xml` next to the `.dll` in the NuGet cache
2. Check which structured query tool is available: `xmllint --version 2>/dev/null`
3. Grep for the type/member name to confirm it exists
4. Use the best available tool to extract the relevant doc block
5. Read `<summary>`, `<param>`, `<returns>`, and `<remarks>` as needed

## Tool selection

Always probe availability before committing to a tool — `xmllint` may be present even on Windows
(e.g. via Git, Chocolatey, or Scoop):

```bash
xmllint --version 2>/dev/null && echo "xmllint available" || echo "xmllint not found"
pwsh -c "Get-Command Select-Xml" 2>/dev/null && echo "pwsh available"
```

Preference order for structured XPath queries:

1. **`xmllint`** — most ergonomic when available (any platform)
2. **`pwsh` Select-Xml** — cross-platform fallback
3. **Grep** — always available; use for quick text lookups

```bash
# xmllint — pretty-print and XPath
xmllint --format MyLib.xml
xmllint --xpath "//member[contains(@name,'UpdateAsync')]" MyLib.xml
```

## Platform notes

| Tool                | Windows                                      | Unix/macOS                        |
| ------------------- | -------------------------------------------- | --------------------------------- |
| Grep                | Available via Git Bash / WSL                 | Native                            |
| `xmllint`           | Not built-in; check first — may be installed | Native (`libxml2-utils`)          |
| `pwsh` (PowerShell) | Native                                       | Install `powershell` package      |
| Python xml.etree    | `py` launcher (`py -c "..."`)                | `python3` (usually pre-installed) |

The NuGet cache path (`~/.nuget/packages/`) works in both bash-on-Windows and Unix shells.
