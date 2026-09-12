import test from 'node:test';
import assert from 'node:assert/strict';
import {normalizeServerAddress} from '../mobile/server-address.ts';

test('server address has one stable origin for requests and saved sessions', () => {
  assert.equal(normalizeServerAddress('  https://RESTAURANT.example.com/  '), 'https://restaurant.example.com');
  assert.equal(normalizeServerAddress('https://restaurant.example.com:8443'), 'https://restaurant.example.com:8443');
});

test('reject addresses that could expose credentials or route requests incorrectly', () => {
  for (const address of ['', 'restaurant.example.com', 'http://restaurant.example.com', 'ftp://restaurant.example.com',
    'https://user:password@restaurant.example.com', 'https://restaurant.example.com/api',
    'https://restaurant.example.com?token=secret', 'https://restaurant.example.com#login']) {
    assert.throws(() => normalizeServerAddress(address), undefined, address);
  }
});
