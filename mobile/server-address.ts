export function normalizeServerAddress(input: string): string {
  let url: URL;
  try { url = new URL(input.trim()); }
  catch { throw new Error('Enter a complete address, such as https://restaurant.example.com.'); }
  if (url.protocol !== 'https:') throw new Error('Use an HTTPS address to keep staff logins secure.');
  if (url.username || url.password || url.search || url.hash || (url.pathname !== '/' && url.pathname !== '')) {
    throw new Error('Enter only the server address, without /api, a page path, or login details.');
  }
  return url.origin;
}
