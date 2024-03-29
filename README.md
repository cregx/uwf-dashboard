# UWF Dashboard
[![CodeQL](https://user-images.githubusercontent.com/14788832/133478909-fa66cf1a-e431-456f-9040-5535829f5796.png)](https://github.com/cregx/uwf-dashboard/actions/workflows/codeql-analysis.yml) [![License](https://img.shields.io/badge/license-MIT-green)](./LICENSE.md) [![docs](https://img.shields.io/badge/docs-available-brightgreen)](https://www.cregx.de/docs/uwfdashboard/) [![Website](https://img.shields.io/website?down_color=lightgrey&down_message=offline&up_color=blue&up_message=online&url=https%3A%2F%2Fwww.cregx.de)](https://www.cregx.de) [![GitHub (Pre-)Release Date](https://img.shields.io/github/release-date-pre/cregx/uwf-dashboard?label=pre-release%20date)](https://github.com/cregx/uwf-dashboard/releases) [![GitHub all releases](https://img.shields.io/github/downloads/cregx/uwf-dashboard/total?color=green&label=download%20all%20releases)](https://github.com/cregx/uwf-dashboard/releases)

The Windows UI application "UWF Dashboard" offers the possibility of a simplified activation/deactivation of an already configured UWF on a local or a remote domain computer.

Normally, one would use the console tool uwfmgr.exe to manage UWF but the UWF Dashboard can take over this task:

![overview-uwfdb](https://www.cregx.de/assets/images/overview-uwfdb.svg)

![no-more-console](https://www.cregx.de/assets/images/noconsole.svg)

![uwf-dashboard-gui-status](https://www.cregx.de/assets/images/uwf-dashboard-gui-status.svg)

**_What is Unified Write Filter:_**

UWF is an optional Windows lockdown feature that helps protect drives by intercepting all writes to a drive (installs, changes) and redirecting them to a virtual overlay. After a system reboot, all changes are undone.
This serves to protect the system.

UWF is often used for **kiosk**, **hotel** computers **or systems** that are to be secured against changes by their users.
For more information about UWF, visit [(https://docs.microsoft.com/en-us/windows-hardware/customize/enterprise/unified-write-filter)](https://docs.microsoft.com/en-us/windows-hardware/customize/enterprise/unified-write-filter).

## More information
For more information on this project, please visit my website: [(https://www.cregx.de/docs/uwfdashboard/)](https://www.cregx.de/docs/uwfdashboard/)

## Still under development...

But note that UWF Dashboard is still under development. At the moment, it only has the basic functionality to activate and deactivate the UWF. The configuration of the UWF environment on the target system must be done using either uwfmgr.exe or PowerShell.

## Code of Conduct

Please refer to the [Code of Conduct](https://github.com/cregx/uwf-dashboard/blob/main/CODE_OF_CONDUCT.md) for this repository.

## Disclaimer

This program code is provided "as is", without warranty or guarantee as to its usability or effects on systems. It may be used, distributed and modified in any manner, provided that the parties agree and acknowledge that the author(s) assume(s) no responsibility or liability for the results obtained by the use of this code.
