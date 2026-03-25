## Table of Contents
<!-- TOC -->

- [Introduction](#introduction)
- [The Goal](#the-goal)
- [The Workflow](#the-workflow)
- [Installing Host Dependencies](#installing-host-dependencies)
  - [Upgrading WinGet](#upgrading-winget)
  - [Installing dependencies using `WinGet`](#installing-dependencies-using-winget)
  - [Installing dependencies](#installing-dependencies)
  - [Installing `your` dependencies](#installing-your-dependencies)

<!-- /TOC -->

# Introduction

A `very opinionated` workflow and set of `PowerShell` `scripts and cmdlets` to support `Developer PC setup` using `WinGet`.

# The Goal

Providing a mean to setup a `Developer PC` `from scratch` by running simple `Powershell` commands.

You might provide your own `git repositories` and `files` where appropriate.

# The Workflow 

* Upgrade `WinGet` application using `msstore`
* Install basics using `WinGet`
* using `Git` to clone the repository and run `Invoke-Me.ps1` script

# Installing Host Dependencies

You need a few things to get started :

* `WinGet`
* `Powershell`
* `Git`

## Upgrading WinGet 

_This will upgrade the `Windows Desktop App Installer` (a.k.a. `WinGet`) using msstore._
_This method has been tested on CI on freshly created `Windows 11 Enterprise Trial`-based `Virtual machine` setup._

Run the following in an `Aministrator`-privileged `Windows Powershell` Terminal` :

```powershell
winget install -s msstore --id 9NBLGGH4NNS1 `
  --force --accept-source-agreements --accept-package-agreements
```

## Installing dependencies using `WinGet`

To function, `FrenchExDev.DevSetup.PoSh` requires you to install  `Git` and `Powershell`.

Run the following in a _new_ `Aministrator`-privileged `terminal` :

```powershell
winget install --id Git.Git Microsoft.Powershell `
  --accept-source-agreements --accept-package-agreements
```

## Installing dependencies

Run the following in a _new_ `Aministrator`-privileged `terminal` :

```powershell
# adapt to your will
$myAcme = "MyAcme"
```

```powershell
# adapt to your will
$devSetupClonePath = "c:/code/$myAcme.MyDevInfra.PoSh/PoSh/FrenchExDev/DevSetup.PoSh"
```

* Via `SSH`

```powershell
$devSetupCloneUrl = "ssh://git@gitlab.frenchexdev.lab:2222/FrenchExDev/DevSetup.PoSh.git"
```

* Via `HTTPS`

```powershell
$devSetupCloneUrl = "https://gitlab.frenchexdev.lab/FrenchExDev/DevSetup.PoSh.git"
```

## Installing `your` dependencies

_If you already have a `$myAcme.DevSetup.PoSh`, you can provide its `https` `git clone` `url`_

```powershell
$myAcmeDevSetupCloneUrl = "https://gitlab.$($myAcme.tolowerinvariant()).lab/$myAcme/DevSetup.PoSh.git"
```

* _Finally_, run the following :

```powershell
git clone -b main $devSetupCloneUrl $devSetupClonePath
Push-Location $devSetupClonePath
Import-Module ./DevSetup.PoSh.psd1
Invoke-DevSetup -Dotnet @('9') -DevSetupClonePath $devSetupClonePath -MyAcmeCloneUrl $myAcmeDevSetupCloneUrl
```
