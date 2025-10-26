<p align="center">
  <img src="/src/third_party/medinilla-flowers-1024x683.jpg" width=70% height=70%>
</p>

# Medinilla - An OCPP Compliant Management System
![DOTNET CI](https://github.com/eliodecolli/Medinilla/actions/workflows/dotnet.yml/badge.svg)

Medinilla is a charging station management system backend built on top of ASP .NET Core. It aims to be a comprehensive, modular platform upon which you can build EV charging solutions.

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
  <img src="/src/third_party/core-arch.jpg" width=70% height=70%>
</p>

## OCPP Implementation
Currently detailed, OCPP Basic Implementation (as per docs):

### Charging Station Bootup
Use cases to be implemented:
- ~~B01~~ - **Done as of 26/10/2025** - NOT Tested E2E ❌
- B02
- ~~B03~~
- ~~B04~~

### Charging Station Configuration
Use cases to be implemented:
- B05
- B06
- B07

### Charging Station Reset
Use cases to be implemented:
- B11
- B12

### Authorization Options
Use cases to be implemented (one of):
- C01
- C02
- C04

### Transaction Mechanism
Use cases to be implemented:
- E01 (one of S1-S6)
- E02
- E03
- E05
- E06 (one of S1-S6)
- E07
- E08
- One of E09, E10, E11, E12, E13

### Availability
Use cases to be implemented:
- G01
- G03
- G04

### Monitoring Events
Use cases to be implemented:
- G05
- N07

### Transaction related Meter values
Use cases to be implemented:
- J02

### Data Transfer
Use cases to be implemented:
- P01
- P02


## Roadmap
Some of the future features in the pipeline for Medinilla are:
- [✓] **~~Transaction Graphs~~**. These are an optimized way of computing transaction consumption, by generating and keeping track of a graph with sampled values based on measurand, context, etc. - [Link to README](https://github.com/eliodecolli/Medinilla/blob/master/Medillina.Services/v1/Transactions/README.md)
- **Custom hardware logic plugin**. Despite OCPP being a comprehensive protocol, firmware implementations vary significantly in practice. Medinilla will offer plug-and-play modules for extending or modifying OCPP event handlers, allowing users to either build firmware-specific plugins or reuse existing ones from the community.
- **HTTP Hooks**. Users can inject custom logic in the pipeline by wiring their own implementation. We send events to your endpoint. You consume them.

## Work overview
Actions have been implemented using the following Charging Station emulators:
1. https://github.com/extrawest/Charge-Point-Simulator-via-OCPP-2.0.1
2. https://evlab.i4b.pl/
3. [EVerest](https://github.com/everest)
