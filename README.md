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
- Security Event
- Authorize
- Transaction Event

## Medinilla - Architecture
The most fundamental idea behind the architectural decisions on this project was scalability, and fault tolerance. The core logic of Medinilla runs in Akka Actors, which are assigned per-client connection.
One thing to note is that response to websockets is dispatched on the thread pool directly, since we don't expect it to grow that much (us sending data back to a ws is essentially 'fire-and-forget').

<p align="center">
  <img src="/third_party/core-arch.jpg" width=70% height=70%>
</p>

### Roadmap
Some of the future features in the pipeline for Medinilla are:
- [✓] **~~Transaction Graphs~~**. These are an optimized way of computing transaction consumption, by generating and keeping track of a graph with sampled values based on measurand, context, etc. - [Link to README](https://github.com/eliodecolli/Medinilla/blob/master/Medillina.Services/v1/Transactions/README.md)
- **Custom hardware logic plugin**. While OCPP is a detailed protocol, actual implementations by charging harware firmwares seem to be quite nuanced. Medinilla will be able to support easy plug-and-play modules, used to extend or modify OCPP event handlers. One can create a "plugin" for a specific firmware implementation, or reuse somebody's else.
- **HTTP Hooks**. Users can inject custom logic in the pipeline by wiring their own implementation. We send events to your endpoint. You consume them.

## Work overview
Actions have been implemented using the following Charging Station emulators:
1. https://github.com/extrawest/Charge-Point-Simulator-via-OCPP-2.0.1
2. https://evlab.i4b.pl/
3. [EVerest](https://github.com/everest)
