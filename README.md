<p align="center">
  <img src="/third_party/medinilla-flowers-1024x683.jpg" width=70% height=70%>
</p>

##
### 🔴 OCA has announced OCPP 2.1, which is set to replace 2.0.1 while maintaining backward compatibility. 🔴
##

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

## Work overview
Actions have been implemented using the following two Charging Station emulators:
1. https://github.com/extrawest/Charge-Point-Simulator-via-OCPP-2.0.1
2. https://evlab.i4b.pl/

I wish I could use more, but most reliable emulators are for 1.6, meanwhile 2.0.1 is lacking a lot in this area. Maybe I should consider just running EVerest and simulating scenarios there.
