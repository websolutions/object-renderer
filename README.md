# WSOL Object Renderer
The Object Renderer is designed to mimic the EPiServer rendering engine that allows views for objects, which can support tagging to change how objects display, or hide them altogether.

## Getting Started
Add WSOL's NuGet feed as a package source in Visual Studio. Below is the feed source:

http://nuget.wsol.com/api/

Instructions for adding package sources can be found at:

https://docs.nuget.org/consume/package-manager-dialog#package-sources

## Build Instructions
Checkout this solution and install the following NuGet packages:
* WSOL.IocContainer - DLL required to run
* WSOL.MSBuild.AutoVersion.Git - required to build only, development dependency only
 
Build and deploy the following files to an Ektron CMS site
* WSOL.ObjectRenderer\bin\WSOL.IoCContainer.dll
* WSOL.ObjectRenderer\bin\ObjectRenderer.dll
 
## Or NuGet Install
Alternatively, obtain the built Nuget package from the WSOL NuGet feed for:

WSOL.ObjectRenderer

## Code Samples
Usage examples can be found in the WSOL.EktronCms.ContentMaker.Samples project source code.

* [Object Views](https://github.com/websolutions/ektroncms-content-maker/tree/master/WSOL.EktronCms.ContentMaker.Samples/Views/ArticleContent)
* [Object Renderer Web Control](https://github.com/websolutions/ektroncms-content-maker/blob/master/WSOL.EktronCms.ContentMaker.Samples/ContentRenderSamples.aspx)
* [Object Renderer Code Behind](https://github.com/websolutions/ektroncms-content-maker/blob/master/WSOL.EktronCms.ContentMaker.Samples/ContentRenderSamples.aspx.cs)

## Important Information

* Views for models are found on web site startup, if any changes are made to TemplateDescriptor attributes for views, the web site will need to be restarted, and the quickest approach is to add a white space at the end of the web.config file in the web site root (this only impacts sites running as web sites and not web apps).
* Views currently support web forms only and not MVC.