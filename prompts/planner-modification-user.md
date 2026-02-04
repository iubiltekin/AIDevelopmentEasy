# Planner modification – user prompt

# MODIFICATION REQUIREMENT

{{REQUIREMENT}}

# TARGET CLASS AND REFERENCES

{{MODIFICATION_CONTEXT}}

{{CODEBASE_SECTION}}

# INSTRUCTIONS

Based on the requirement and the class/reference information above:

1. Create tasks to modify the primary class first
2. Create tasks to update all affected files that reference this class
3. Ensure each task has clear, specific instructions for what to change
4. Order tasks by dependency (primary class first, then consumers)
5. Keep existing code intact - only modify what's necessary

For each file that needs changes, create a separate task with:

- Clear description of what to modify
- Which methods/properties are affected
- How the existing code should be preserved

Output your plan as JSON.
