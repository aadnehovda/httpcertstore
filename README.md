# Caddy HTTP certificate store

This is a simple implementation of a Caddy compatible certificate manager that gets certificates and keys from the Windows Certificate Store.
A certificate manager for Caddy which can read certificates from the Windows Windows Certificate Store.

Using a Caddyfile like this
```
{
  auto_https off
}
test.local.gd {
  tls {
    get_certificate http http://127.0.0.1:27510/LocalMachine/My
  }
  respond "Hello, world!"
}
```