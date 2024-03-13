![DOTNET CI](https://github.com/eliodecolli/Medinilla/actions/workflows/dotnet.yml/badge.svg)

# Medinilla - An OCPP Compliant Management System
Medinilla is a charging station management system backend built on top of ASP .NET Core. This project aims to be part of a proprietary system for the time being. This is subject to change tbh.
The server itself is supposed to be a monolith, I don't expect it to grow that much in terms of complexity to warrant a split into microservices.

Currently implemented OCPP messages:
- Boot Notification
- Heartbeat
