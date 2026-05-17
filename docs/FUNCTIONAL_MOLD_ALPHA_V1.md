# Functional Mold Alpha v1

Functional Mold Alpha v1 is the current closed milestone for PicoMoldForge.

It does not certify a production mold. It proves that PicoMoldForge now has a bounded, verifiable alpha pipeline for preliminary mold generation and engineering validation.

## Current Functional Alpha Capabilities

The alpha system includes:

- core/cavity separation validation
- mold separation engine
- parting plane scoring
- shutoff strategy contract
- basic draft geometry analysis
- voxel wall-thickness analysis
- undercut risk analysis
- cooling channel subtraction planning
- gate/runner/sprue generation planning
- ejector candidate generation
- clearance collision matrix
- integrated FunctionalMoldAlphaReport
- final project report integration
- one-command alpha verification script

## Verification Command

Run:

    powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify-alpha.ps1

The verifier checks:

- required alpha source artifacts exist
- alpha files are non-empty
- core tests pass
- FunctionalMoldAlphaReport tests pass
- baseline passes
- optional generator smoke produces FinalProjectReport.json when generator project and sample config are available

## Definition of Done

Functional Mold Alpha v1 is done when:

- verify-alpha.ps1 passes
- verify-baseline.ps1 passes
- git status is clean
- the repo documents that generated geometry is still preliminary
- GitHub origin/main contains the final alpha commit

## Important Limitation

Functional Mold Alpha v1 is not a certified tooling package.

It still requires:

- qualified mold engineer review
- manufacturing review
- mold-flow analysis
- true CAD/boolean integration hardening
- validated shutoff surfaces
- real trial data
- customer/toolmaker signoff

## Next Work After This Series

The next series should not add more isolated contracts first.

The next major series should connect the alpha contracts to real geometry generation:

1. connect alpha metrics into the generator output path
2. produce alpha report fields from actual generated artifacts
3. connect cooling/gate/ejector geometry to STL/boolean generation
4. improve parting and shutoff geometry beyond preliminary contracts
5. create CI or release workflow for alpha verification