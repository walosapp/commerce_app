import { describe, expect, it } from 'vitest';
import { validatePassword } from '../../utils/passwordPolicy';

describe('passwordPolicy shared ASCII contract', () => {
  it.each([
    'Short1!',
    'alllowercase1!',
    'ALLUPPERCASE1!',
    'NoNumber!',
    'NoSpecial1',
    'Has Space1!',
    'Has\tTab1!',
    'HasLine1!\n',
    'Ábcdef1!',
    'Abcdef1😀',
  ])('rejects %s', (password) => {
    expect(validatePassword(password)).toEqual(expect.any(String));
  });

  it.each(['Valid1!x', 'A1!bcdef', 'Zz9#1234'])('accepts %s', (password) => {
    expect(validatePassword(password)).toBeNull();
  });
});
