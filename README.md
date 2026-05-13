# <span>AS4.NET Core [WORK IN PROGRESS]</span>

## Introduction

<span>AS4.NET Core</span> is an open-source application that implements the OASIS AS4 specification. It supports both the e-SENS e-Delivery and the EESSI AS4 Messaging profile as an ebMS endpoint.  

Since version v3.0.0, <span>AS4.NET</span> can also act as an intermediary MSH (i-MSH) with message forwarding support and MEP bridging.

The component has been conformance tested against the e-SENS eDelivery specifications.  
Testing against the EESSI AS4 Messaging Profile has also been conducted.

<span>AS4.NET Core</span> is interoperable with multiple other AS4 gateway providers; <span>AS4.NET</span> has undergone performance and interop-tests against Holodeck B2B, RSSBus, Domibus, Flame Message Server and IBM B2B.

## Documentation

A configuration- and usermanual can be found [online](https://ec.europa.eu/cefdigital/wiki/display/EDELCOMMUNITY/AS4.NET).

## Project Summary

This is an Enterprise eDelivery AS4 (Applicability Statement 4) messaging system for .NET Core, a standards-based B2B/EDI message exchange platform following OASIS specifications. It handles secure, reliable asynchronous electronic messaging between organizations.

### Core Components

1. Agent-Based Message Processing Pipeline

    - Background service agents (Submit, Receive, Deliver, Notify, PullSend, PushSend, Forward) continuously process messages
    - Each agent follows: Receiver → Transformer → StepExecutioner → ExceptionHandler → JournalLogger
    - Decoupled, scalable architecture for handling different message flows

2. Message Entities

    - InMessage/OutMessage: Core entities representing incoming/outgoing AS4 messages with lifecycle tracking
    - Properties: EBMS IDs, routing (From/To parties), service/action, processing mode, message status, SOAP envelope
    - Base entity class handles ID, insertion/modification timestamps

3. AS4 Message Model

    - Structured objects: UserMessage (business data), SignalMessage (receipts/errors), Attachment (payloads)
    - Metadata: CollaborationInfo (service/action), SecurityHeader (encryption/signing), Party identifiers
    - Support for message properties and multi-hop messaging

4. Processing Modes (PMode)

    - XML configuration objects controlling how messages are secured, routed, and processed
    - SendingProcessingMode & ReceivingProcessingMode stored as serialized XML in message entities

5. Step-Based Execution Engine

    - Pipeline pattern: Steps are individual processing units composed into chains
    - Features: CompositeStep (grouped steps), ConditionalStep (branching), StepResult tracking

6. Comprehensive Services

    - Message services (CRUD, status updates)
    - Exception handling and retry logic
    - Certificate management
    - Receipt/error piggybacking (bundling)
    - File storage for message bodies

7. Database Layer

    - Entity Framework Core 8.0 with multi-database support (SQL Server, SQLite, In-Memory)
    - 6+ migrations for schema evolution
    - Repositories abstract data access

8. HTTP & Resilience

    - Polly-based retry policies for reliable HTTP delivery
    - Support for dynamic service discovery (SMP configuration)

9. Standards Compliance

    - OASIS EbMS 3.0 XML namespaces and structure
    - SOAP 1.1/1.2 envelopes
    - RSA-SHA256 digital signatures
    - Multi-hop messaging support

Key Design Patterns: Dependency Injection (Microsoft.Extensions), Repository Pattern, Pipeline/Step Pattern, Background Service Pattern, Configuration Management with dynamic PMode watchers.

## Features

### v5.0.0

- Rename from <span>AS4.NET</span> to <span>AS4.NET Core</span>
- .NET 8.0 support
  - built-in logging API (open for multiple logging providers, like NLog, Serilog, etc.)
  - built-in dependency injection
  - built-in configuration and options API
  - built-in hosting API
- New Agent initialization and configuration system based on the .NET configuration and options API
- Updated internal messaging engine based on the .NET hosting and dependency injection system
- Updated internal message processing pipeline based on the .NET hosting and dependency injection system

### v4.0.1

- DynamicDiscovery settings that can be defined in the Sending PMode are no longer case-sensitive
- Bugfix: Send retry-functionality (receptionawareness) was not working in v4.0.0 when `IsMultihop` was enabled in the Sending PMode.  This issue is fixed in v4.0.1.
- Bugfix: When AS4.NET v4.0.0 is configured to receive messages via pulling, response signal messages were created but were never sent.  This issue is fixed in v4.0.1.

### v4.0.0

- Support for the OASIS BDX dynamic discovery profile
- Support for sending response signal messages via reliable piggybacking in a pull receive scenario
- Allow the AS4.NET Windows Service MSH to be installed via an MSI
- Control the AS4.NET Windows Service MSH via a system tray application
- Improved Receiving PMode matching proces when multiple from-parties / to-parties are specified in the AS4 Message or in the Receiving PMode
- Allow dynamic discovery based on the sender information in the SubmitMessage or in case of a forwarding scenario on the sender information in the AS4 Message
- Support for internal journal logging to track down operations executed on the message (compress/decompress, signing/verify, encrypt/decrypt)
- Support for receiving bundled message units
- Configurable submit payload retrieval path location
- Configurable pull authorization map path location
- Improvements in the web interface for pmode and agents configuration
- Improvements to the internal messaging engine

> This version doesn't support **Sending PModes** anymore as a way to respond to AS4 messages but uses the **Receiving PMode** for this. Please update your **Receiving PModes**, for more information see: [Remove Sending PMode as responding PMode](output/doc/wiki/runtime/configuration/remove-response-pmode.md).

### v3.1.0

- Retry functionality for deliver operation
- Retry functionality for notify operation
- Static Receive support
- Improvements in the web interface for configuration
- Improvements in the internal messaging engine

### v3.0.0

- Intermediary MSH functionality with message forwarding including MEP bridging support
- Static Submit support
- Possibility to run the <span>AS4.NET</span> MSH as a Windows Service
- Improved Dynamic Discovery implementation
- Dynamic Forwarding support
- Improved high availability support
- Support for Non-Repudiation of Receipt verification
- Support for automatic Message Cleanup
- Optionally allow that a message is signed with a certificate coming from an unknown CA authority when verifying message signatures
- Web interface for SMP Routing Configuration
- Improvements in the web interface for configuration
- Improvements to the internal messaging engine

### v2.0.1

- Configurable payload naming when delivering on filesystem
- Continued performance tuning for large messages and high volume processing

### v2.0.0

- Web interface for configuration
- Web interface for monitoring
- Web interface for user management
- Web interface for testing
- One-Way/Pull pattern as responder
- Support for sub-channels
- Support for message forwarding
- Support for MEP bridging
- Support for PullRequest authorization
- Support for SMP/SML dynamic discovery
- Support for TLS server side
- Continued performance tuning for large messages, up to 3GB
- Continued performance tuning for high volume processing
- Improvements to the internal messaging engine

### v1.1.0

- Submit, deliver and notify via HTTP protocol
- Submit and deliver attachments via HTTP protocol
- One-Way/Pull pattern as initiator
- Support for sub channels in One-Way/Pull pattern
- Support for multi-hop AS4 profile
- Support for TLS client certificates
- Performance tuning for large messages, up to 2GB
- Performance tuning for high volume processing

### v1.0.0

- One-Way/Push message exchange pattern
- XML based configuration
- XML based PMode configuration
- Dynamic PMode override
- Multiple submit, notify and deliver agents
- FILE based receivers and senders
- Signing and encryption using WS-Security
- AS4 Compression
- AS4 Reception Awareness and Retry
- AS4 Duplicate Detection and Elimination

## Third Party software

The following third party libraries are used by <span>AS4.NET Core</span> runtime:

- [BouncyCastle.Cryptography](https://www.bouncycastle.org/stable/nuget/csharp/website) ([MIT License](https://licenses.nuget.org/MIT))
- [FluentValidation](https://docs.fluentvalidation.net/en/latest) ([Apache 2.0 License](https://licenses.nuget.org/Apache-2.0))
- [Heijden.Dns.Portable](https://github.com/softlion/Heijden.Dns) ([MIT License](https://opensource.org/licenses/MIT))
- [MimeKitLite](https://mimekit.net) ([MIT License](https://licenses.nuget.org/MIT))
- [Nlog](https://nlog-project.org) ([BSD 3 License](https://licenses.nuget.org/BSD-3-Clause))
- [Polly](https://github.com/App-vNext/Polly) ([BSD 3 License](https://licenses.nuget.org/BSD-3-Clause))
- [Scrutor](https://github.com/khellang/Scrutor) ([MIT License](https://licenses.nuget.org/MIT))
- [System.Linq.Dynamic.Core](https://dynamic-linq.net) ([Apache 2.0 License](https://licenses.nuget.org/Apache-2.0))
- [SQLite](https://sqlite.org) ([Public Domain](https://sqlite.org/copyright.html))

The following third party libraries are used by <span>AS4.NET Core</span> tests:

- [FsCheck.Xunit.v3](https://fscheck.github.io/FsCheck) ([BSD 3 License](https://licenses.nuget.org/BSD-3-Clause))
- [Moq](https://github.com/devlooped/moq) ([BSD 3 License](https://licenses.nuget.org/BSD-3-Clause))
- [NSubstitute](https://nsubstitute.github.io) ([BSD 3 License](https://licenses.nuget.org/BSD-3-Clause))
- [xunit.v3](https://xunit.net) ([Apache 2.0 License](https://licenses.nuget.org/Apache-2.0))

## License

This software is licensed under the [EUPL License v1.1](https://joinup.ec.europa.eu/community/eupl/og_page/european-union-public-licence-eupl-v11).
