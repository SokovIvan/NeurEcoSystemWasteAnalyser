# Ecological System of neural analysis of data to calssificate waste

A dual-component application designed for intelligent waste analysis and visualization. This project combines the power of Python-based machine learning with a robust C# desktop interface, communicating seamlessly via TCP sockets.

## Architecture

- ML Model (Python): Built using modern deep learning frameworks. Responsible for image processing and classification.
- Visualization App (C#): Developed for Windows, offering real-time data display and user interaction.
- Communication: Inter-process communication is established via TCP Sockets** using JSON messages for structured data exchange.

## Installation
### Prerequisites
- Python 3.8+
- .NET 6.0+ (for C# application)
- Required Python packages (torch, numpy, etc.)
### Setup
- Clone the repository.
- Install Python dependencies.
- Run the Python server first, then launch the C# client.
