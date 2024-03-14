<p align="center">
  <img src="/third_party/medinilla-flowers-1024x683.jpg" width=70% height=70%>
</p>

# Medinilla - An OCPP Compliant Management System
![DOTNET CI](https://github.com/eliodecolli/Medinilla/actions/workflows/dotnet.yml/badge.svg)

Medinilla is a charging station management system backend built on top of ASP .NET Core. This project aims to be part of a proprietary system for the time being. This is subject to change tbh.
The server itself is supposed to be a monolith, I don't expect it to grow that much in terms of complexity to warrant a split into microservices.

Currently implemented OCPP messages:
- Boot Notification
- Heartbeat
