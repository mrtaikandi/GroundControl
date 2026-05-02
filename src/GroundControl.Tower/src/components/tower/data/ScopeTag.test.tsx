import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ScopeTag } from './ScopeTag';

describe('ScopeTag', () => {
  it('renders dimension and value separated by equals', () => {
    render(<ScopeTag dimension="Environment" value="dev" />);

    expect(screen.getByText('Environment')).toBeInTheDocument();
    expect(screen.getByText('=')).toBeInTheDocument();
    expect(screen.getByText('dev')).toBeInTheDocument();
  });
});
