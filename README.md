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

## Work overview
While actions themselves are "implemented" in a sense that they allow a communication between CSMS-CS, they still do not follow any business logic (or even OCPP rules for that matter). They simply make sure that the charging stations receives at least something back to keep the connection alive. Ie, during the authorization process, the CS receives an `Accepted` status, but there are no checks in place to make sure the transactions can begin.
Another thing we should do, is fucking make some unit tests for each action. At this moment, it makes no sense since those actions aren't properly implemented, but in the future unit testing is the way we'll make sure everything is up-to-date.
