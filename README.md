# Dynamic DNS App for Technitium DNS Server

A comprehensive Dynamic DNS service app for Technitium DNS Server with user registration and payment system integration.

## Features

- **Dynamic DNS Updates**: Automatically update DNS records when IP addresses change
- **User Registration System**: Allow users to create accounts and manage their domains
- **Subscription Plans**: Free, Basic, and Pro plans with different features and limits
- **Payment Integration**: Stripe payment processing for subscription management
- **API Access**: Secure API for programmatic access to the Dynamic DNS service
- **Web Interface**: User-friendly web interface for managing domains and account
- **IPv4 and IPv6 Support**: Full support for both IPv4 and IPv6 addresses

## Installation

### Prerequisites

- Technitium DNS Server v8.0 or higher
- .NET 8.0 SDK
- Stripe account (optional, for payment processing)

### Installation Steps

1. Clone this repository or download the release package
2. Build the project:
   ```
   dotnet build
   ```
3. Copy the built files to the Technitium DNS Server Apps directory:
   ```
   cp -r bin/Debug/net8.0/* /path/to/dns/server/apps/DynamicDnsApp/
   ```
4. Restart the Technitium DNS Server or reload apps from the web console

### Configuration

After installation, you can configure the app through the Technitium DNS Server web console:

1. Go to Apps > Dynamic DNS App
2. Configure the settings:
   - Web Service Port: Port for the web interface (default: 8080)
   - Database Path: Path to the SQLite database file
   - Stripe API Key: Your Stripe secret key (for payment processing)
   - Subscription Plans: Configure pricing and domain limits
   - Email Settings: Configure SMTP for email notifications

## Usage

### Setting Up Dynamic DNS

1. Create an account through the web interface
2. Add a domain to your account
3. Use the provided update URL to update your IP address:
   ```
   https://your-dns-server:8080/api/dynamicdns/update?domain=yourdomain.example.com&token=your-update-token
   ```

### Update Methods

#### Using a Web Browser

Simply visit the update URL in your browser:
```
https://your-dns-server:8080/api/dynamicdns/update?domain=yourdomain.example.com&token=your-update-token
```

#### Using curl

```bash
curl "https://your-dns-server:8080/api/dynamicdns/update?domain=yourdomain.example.com&token=your-update-token"
```

#### Using wget

```bash
wget -q -O - "https://your-dns-server:8080/api/dynamicdns/update?domain=yourdomain.example.com&token=your-update-token"
```

#### Specifying IP Addresses

You can explicitly specify IPv4 and/or IPv6 addresses:

```
https://your-dns-server:8080/api/dynamicdns/update?domain=yourdomain.example.com&token=your-update-token&ipv4=203.0.113.1&ipv6=2001:db8::1
```

If no IP addresses are specified, the client's IP address will be used.

### API Access

The app provides a RESTful API for programmatic access. You can create API keys in the web interface and use them to authenticate API requests.

Example API endpoints:

- `GET /api/dynamicdns/update`: Update a domain's IP address
- `POST /api/dynamicdns/create`: Create a new domain
- `GET /api/dynamicdns/list`: List all domains for a user
- `DELETE /api/dynamicdns/{id}`: Delete a domain

## DNS Server Integration

The app integrates with Technitium DNS Server through the APP record type. To use the Dynamic DNS service:

1. Create a primary zone in the DNS server
2. Add an APP record pointing to the Dynamic DNS app
3. Configure the APP record data as needed

Example APP record data:
```json
{
  "allowedNetworks": ["0.0.0.0/0", "::/0"],
  "requireAuth": true
}
```

## Development

### Project Structure

- `App.cs`: Main application class implementing IDnsApplication and IDnsAppRecordRequestHandler
- `Models/`: Data models for users, domains, API keys, etc.
- `Services/`: Business logic services
- `Controllers/`: API controllers
- `Data/`: Database context and migrations
- `web/`: Web interface files (HTML, CSS, JavaScript)

### Building from Source

1. Clone the repository
2. Restore dependencies:
   ```
   dotnet restore
   ```
3. Build the project:
   ```
   dotnet build
   ```

## License

This project is licensed under the GNU General Public License v3.0 - see the LICENSE file for details.

## Acknowledgments

- [Technitium DNS Server](https://technitium.com/dns/)
- [Stripe](https://stripe.com/) for payment processing
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) for data access